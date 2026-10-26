using EnterpriseGenAI.DocumentIngestion.Worker.Application;
using EnterpriseGenAI.DocumentIngestion.Worker.Contracts;
using EnterpriseGenAI.DocumentIngestion.Worker.Infrastructure;
using EnterpriseGenAI.DocumentIngestion.Worker.Options;
using Npgsql;
using NpgsqlTypes;

namespace EnterpriseGenAI.DocumentIngestion.Worker.Tests;

public sealed class IngestionJobClaimTests
{
    private readonly string _connectionString =
        Environment.GetEnvironmentVariable("INGESTION_TEST_CONNECTION") ?? throw new InvalidOperationException("Set INGESTION_TEST_CONNECTION.");

    [Fact]
    public async Task Primary_claim_moves_job_and_document()
    {
        var seed = await SeedAsync(jobStatus: "Queued", documentStatus: "Queued");
        try
        {
            await using var dataSource = NpgsqlDataSource.Create(_connectionString);
            var claimer = CreateClaimer(dataSource);

            var result = await claimer.TryClaimAsync(seed.Message, CancellationToken.None);

            var acquired = Assert.IsType<IngestionClaimResult.Acquired>(result);
            Assert.Equal(seed.JobId, acquired.JobId);
            Assert.NotEqual(Guid.Empty, acquired.LeaseToken);

            var row = await ReadAsync(seed);
            Assert.Equal("Running", row.JobStatus);
            Assert.Equal("Processing", row.DocumentStatus);
            Assert.Equal(1, row.AttemptCount);
            Assert.Equal(acquired.LeaseToken, row.LeaseToken);
            Assert.Equal(acquired.LeaseUntil, row.LeaseUntil);
            Assert.Equal(1, row.JobContentVersion);
            Assert.Equal(1, row.JobProcessingVersion);

            var remaining = await LeaseRemainingAsync(seed.JobId);
            Assert.InRange(remaining, TimeSpan.FromSeconds(100), TimeSpan.FromSeconds(120));
        }
        finally
        {
            await DeleteAsync(seed);
        }
    }

    [Fact]
    public async Task Concurrent_claims_succeed_once()
    {
        var seed = await SeedAsync(jobStatus: "Queued", documentStatus: "Queued");
        try
        {
            await using var first = NpgsqlDataSource.Create(_connectionString);
            await using var second = NpgsqlDataSource.Create(_connectionString);

            var results = await Task.WhenAll(
                CreateClaimer(first).TryClaimAsync(seed.Message, CancellationToken.None),
                CreateClaimer(second).TryClaimAsync(seed.Message, CancellationToken.None));

            Assert.Single(results, static result => result is IngestionClaimResult.Acquired);
            Assert.Single(results, static result => result is IngestionClaimResult.AlreadyRunning);

            var row = await ReadAsync(seed);
            Assert.Equal(1, row.AttemptCount);
            Assert.NotNull(row.LeaseToken);

            var winner = results.OfType<IngestionClaimResult.Acquired>().Single();
            Assert.Equal(winner.LeaseToken, row.LeaseToken);
        }
        finally
        {
            await DeleteAsync(seed);
        }
    }

    [Theory]
    [InlineData("Succeeded", "Ready")]
    [InlineData("Succeeded", "Processing")]
    public async Task Succeeded_redelivery_is_already_completed_and_changes_nothing(string jobStatus, string documentStatus)
    {
        var token = Guid.CreateVersion7();
        var seed = await SeedAsync(jobStatus, documentStatus, attemptCount: 2, leaseToken: token, leaseOffsetSeconds: 30);
        try
        {
            var before = await ReadAsync(seed);
            await using var dataSource = NpgsqlDataSource.Create(_connectionString);

            var result = await CreateClaimer(dataSource).TryClaimAsync(seed.Message, CancellationToken.None);

            var completed = Assert.IsType<IngestionClaimResult.AlreadyCompleted>(result);
            Assert.Equal(seed.JobId, completed.JobId);
            Assert.Equal(before, await ReadAsync(seed));
        }
        finally
        {
            await DeleteAsync(seed);
        }
    }

    [Theory]
    [InlineData("Processing", null)]
    [InlineData("Queued", null)]
    [InlineData("Processing", -30)]
    public async Task Running_redelivery_does_not_issue_a_new_lease(string documentStatus, int? leaseOffsetSeconds)
    {
        var token = Guid.CreateVersion7();
        var seed = await SeedAsync(
            "Running",
            documentStatus,
            attemptCount: 3,
            leaseToken: token,
            leaseOffsetSeconds: leaseOffsetSeconds ?? 40);
        try
        {
            var before = await ReadAsync(seed);
            await using var dataSource = NpgsqlDataSource.Create(_connectionString);

            var result = await CreateClaimer(dataSource).TryClaimAsync(seed.Message, CancellationToken.None);

            Assert.IsType<IngestionClaimResult.AlreadyRunning>(result);
            Assert.Equal(before, await ReadAsync(seed));
            Assert.Equal(token, (await ReadAsync(seed)).LeaseToken);
        }
        finally
        {
            await DeleteAsync(seed);
        }
    }

    [Theory]
    [InlineData("Queued", 99, false)]
    [InlineData("Succeeded", 99, false)]
    [InlineData("Succeeded", 1, true)]
    public async Task Mismatched_version_or_deleted_document_is_stale(string jobStatus, int messageProcessingVersion, bool deleted)
    {
        var seed = await SeedAsync(jobStatus, jobStatus == "Succeeded" ? "Ready" : "Queued", deleted: deleted, attemptCount: 1);
        try
        {
            var before = await ReadAsync(seed);
            var message = seed.Message with { ProcessingVersion = messageProcessingVersion };
            await using var dataSource = NpgsqlDataSource.Create(_connectionString);

            var result = await CreateClaimer(dataSource).TryClaimAsync(message, CancellationToken.None);

            Assert.IsType<IngestionClaimResult.Stale>(result);
            Assert.Equal(before, await ReadAsync(seed));
        }
        finally
        {
            await DeleteAsync(seed);
        }
    }

    [Theory]
    [InlineData("Failed")]
    [InlineData("Superseded")]
    [InlineData("Cancelled")]
    public async Task Terminal_job_is_not_reopened(string jobStatus)
    {
        var seed = await SeedAsync(jobStatus, "Failed", attemptCount: 4);
        try
        {
            var before = await ReadAsync(seed);
            await using var dataSource = NpgsqlDataSource.Create(_connectionString);

            var result = await CreateClaimer(dataSource).TryClaimAsync(seed.Message, CancellationToken.None);

            var terminal = Assert.IsType<IngestionClaimResult.Terminal>(result);
            Assert.Equal(jobStatus, terminal.Status);
            Assert.Equal(before, await ReadAsync(seed));
        }
        finally
        {
            await DeleteAsync(seed);
        }
    }

    [Theory]
    [InlineData(3600)]
    [InlineData(-60)]
    public async Task Retry_wait_is_not_started(int nextAttemptOffsetSeconds)
    {
        var seed = await SeedAsync("RetryWait", "Queued", attemptCount: 2, nextAttemptOffsetSeconds: nextAttemptOffsetSeconds);
        try
        {
            var before = await ReadAsync(seed);
            await using var dataSource = NpgsqlDataSource.Create(_connectionString);

            var result = await CreateClaimer(dataSource).TryClaimAsync(seed.Message, CancellationToken.None);

            var scheduled = Assert.IsType<IngestionClaimResult.RetryScheduled>(result);
            Assert.Equal(before.NextAttemptAt, scheduled.NextAttemptAt);
            Assert.Equal(before, await ReadAsync(seed));
        }
        finally
        {
            await DeleteAsync(seed);
        }
    }

    [Fact]
    public async Task Document_not_queued_is_not_claimable()
    {
        var seed = await SeedAsync("Queued", "Uploaded");
        try
        {
            var before = await ReadAsync(seed);
            await using var dataSource = NpgsqlDataSource.Create(_connectionString);

            var result = await CreateClaimer(dataSource).TryClaimAsync(seed.Message, CancellationToken.None);

            Assert.IsType<IngestionClaimResult.NotClaimable>(result);
            Assert.Equal(before, await ReadAsync(seed));
        }
        finally
        {
            await DeleteAsync(seed);
        }
    }

    [Fact]
    public async Task Completed_job_with_another_document_id_is_stale()
    {
        var seed = await SeedAsync("Succeeded", "Ready", attemptCount: 1);
        var otherDocumentId = Guid.CreateVersion7();
        try
        {
            await using (var dataSource = NpgsqlDataSource.Create(_connectionString))
            await using (var connection = await dataSource.OpenConnectionAsync())
            await using (var command = new NpgsqlCommand("""
                INSERT INTO documents (
                    "DocumentId", "OwnerId", "FileName", "ContentType", "Size", "TemporaryObjectKey",
                    "ContentVersion", "ProcessingVersion", "Status", "UploadExpiresAtUtc", "CreatedAt", "UpdatedAt")
                VALUES (
                    @documentId, 'test-owner', 'other.txt', 'text/plain', 1, @temporaryKey,
                    1, 1, 'Ready', clock_timestamp() + INTERVAL '1 hour', clock_timestamp(), clock_timestamp())
                """, connection))
            {
                command.Parameters.AddWithValue("documentId", otherDocumentId);
                command.Parameters.AddWithValue("temporaryKey", $"tmp/{otherDocumentId:N}");
                await command.ExecuteNonQueryAsync();
            }

            var before = await ReadAsync(seed);
            await using var claimSource = NpgsqlDataSource.Create(_connectionString);
            var result = await CreateClaimer(claimSource).TryClaimAsync(seed.Message with { DocumentId = otherDocumentId }, CancellationToken.None);

            Assert.IsType<IngestionClaimResult.Stale>(result);
            Assert.Equal(before, await ReadAsync(seed));
        }
        finally
        {
            await DeleteAsync(seed);
            await using var dataSource = NpgsqlDataSource.Create(_connectionString);
            await using var connection = await dataSource.OpenConnectionAsync();
            await using var command = new NpgsqlCommand("""DELETE FROM documents WHERE "DocumentId" = @documentId""", connection);
            command.Parameters.AddWithValue("documentId", otherDocumentId);
            await command.ExecuteNonQueryAsync();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Missing_job_or_document_is_not_found(bool missingDocument)
    {
        var seed = await SeedAsync("Queued", "Queued");
        try
        {
            var before = await ReadAsync(seed);
            var message = missingDocument
                ? seed.Message with { DocumentId = Guid.CreateVersion7() }
                : seed.Message with { JobId = Guid.CreateVersion7() };
            await using var dataSource = NpgsqlDataSource.Create(_connectionString);

            var result = await CreateClaimer(dataSource).TryClaimAsync(message, CancellationToken.None);

            Assert.IsType<IngestionClaimResult.NotFound>(result);
            Assert.Equal(before, await ReadAsync(seed));
        }
        finally
        {
            await DeleteAsync(seed);
        }
    }

    [Fact]
    public async Task Database_outage_is_not_a_claim_result()
    {
        await using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=none;Password=none;Database=none;Timeout=2;Command Timeout=2");
        var claimer = CreateClaimer(dataSource);
        var message = new DocumentIngestionRequested(Guid.CreateVersion7(), Guid.CreateVersion7(), 1, 1);

        await Assert.ThrowsAnyAsync<NpgsqlException>(() => claimer.TryClaimAsync(message, CancellationToken.None));
    }

    private static PostgresIngestionJobClaimer CreateClaimer(NpgsqlDataSource dataSource) =>
        new(dataSource, Microsoft.Extensions.Options.Options.Create(new IngestionLeaseOptions { LeaseSeconds = 120 }));

    private async Task<Seed> SeedAsync(
        string jobStatus,
        string documentStatus,
        int attemptCount = 0,
        Guid? leaseToken = null,
        int? leaseOffsetSeconds = null,
        int? nextAttemptOffsetSeconds = null,
        bool deleted = false)
    {
        var documentId = Guid.CreateVersion7();
        var jobId = Guid.CreateVersion7();

        await using var dataSource = NpgsqlDataSource.Create(_connectionString);
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            INSERT INTO documents (
                "DocumentId", "OwnerId", "FileName", "ContentType", "Size", "TemporaryObjectKey",
                "ContentVersion", "ProcessingVersion", "Status", "UploadExpiresAtUtc", "DeletedAt", "CreatedAt", "UpdatedAt")
            VALUES (
                @documentId, 'test-owner', 'claim.txt', 'text/plain', 1, @temporaryKey,
                1, 1, @documentStatus, clock_timestamp() + INTERVAL '1 hour',
                CASE WHEN @deleted THEN clock_timestamp() ELSE NULL END,
                clock_timestamp(), clock_timestamp());

            INSERT INTO ingestion_jobs (
                "JobId", "DocumentId", "ContentVersion", "ProcessingVersion",
                "ModelId", "ModelRevision", "Status", "AttemptCount", "LeaseToken", "LeaseUntil", "NextAttemptAt", "CreatedAt", "UpdatedAt")
            VALUES (
                @jobId, @documentId, 1, 1,
                'test-model', 'test-revision', @jobStatus, @attemptCount, @leaseToken,
                CASE WHEN @leaseOffset IS NULL THEN NULL ELSE clock_timestamp() + (@leaseOffset * INTERVAL '1 second') END,
                CASE WHEN @nextOffset IS NULL THEN NULL ELSE clock_timestamp() + (@nextOffset * INTERVAL '1 second') END,
                clock_timestamp(), clock_timestamp());
            """, connection);

        command.Parameters.AddWithValue("documentId", documentId);
        command.Parameters.AddWithValue("jobId", jobId);
        command.Parameters.AddWithValue("temporaryKey", $"tmp/{documentId:N}");
        command.Parameters.AddWithValue("documentStatus", documentStatus);
        command.Parameters.AddWithValue("jobStatus", jobStatus);
        command.Parameters.AddWithValue("attemptCount", attemptCount);
        command.Parameters.AddWithValue("deleted", deleted);
        command.Parameters.Add(new NpgsqlParameter("leaseToken", NpgsqlDbType.Uuid) { Value = (object?)leaseToken ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("leaseOffset", NpgsqlDbType.Integer) { Value = (object?)leaseOffsetSeconds ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("nextOffset", NpgsqlDbType.Integer) { Value = (object?)nextAttemptOffsetSeconds ?? DBNull.Value });
        await command.ExecuteNonQueryAsync();

        return new Seed(jobId, documentId, new DocumentIngestionRequested(jobId, documentId, 1, 1));
    }

    private async Task<Row> ReadAsync(Seed seed)
    {
        await using var dataSource = NpgsqlDataSource.Create(_connectionString);
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            SELECT j."Status", j."AttemptCount", j."LeaseToken", j."LeaseUntil",
                   j."ContentVersion", j."ProcessingVersion", j."NextAttemptAt",
                   d."Status", d."ContentVersion", d."ProcessingVersion", d."DeletedAt"
            FROM ingestion_jobs j
            JOIN documents d ON d."DocumentId" = j."DocumentId"
            WHERE j."JobId" = @jobId
            """, connection);
        command.Parameters.AddWithValue("jobId", seed.JobId);

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return new Row(
            reader.GetString(0),
            reader.GetInt32(1),
            reader.IsDBNull(2) ? null : reader.GetGuid(2),
            ReadUtc(reader, 3),
            reader.GetInt32(4),
            reader.GetInt32(5),
            ReadUtc(reader, 6),
            reader.GetString(7),
            reader.GetInt32(8),
            reader.GetInt32(9),
            ReadUtc(reader, 10));
    }

    private async Task<TimeSpan> LeaseRemainingAsync(Guid jobId)
    {
        await using var dataSource = NpgsqlDataSource.Create(_connectionString);
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            SELECT "LeaseUntil" - clock_timestamp()
            FROM ingestion_jobs
            WHERE "JobId" = @jobId
            """, connection);
        command.Parameters.AddWithValue("jobId", jobId);
        var value = await command.ExecuteScalarAsync();
        return Assert.IsType<TimeSpan>(value);
    }

    private async Task DeleteAsync(Seed seed)
    {
        await using var dataSource = NpgsqlDataSource.Create(_connectionString);
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            DELETE FROM ingestion_jobs WHERE "JobId" = @jobId OR "DocumentId" = @documentId;
            DELETE FROM documents WHERE "DocumentId" = @documentId;
            """, connection);
        command.Parameters.AddWithValue("jobId", seed.JobId);
        command.Parameters.AddWithValue("documentId", seed.DocumentId);
        await command.ExecuteNonQueryAsync();
    }

    private static DateTime? ReadUtc(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : DateTime.SpecifyKind(reader.GetDateTime(ordinal), DateTimeKind.Utc);

    private sealed record Seed(Guid JobId, Guid DocumentId, DocumentIngestionRequested Message);

    private sealed record Row(
        string JobStatus,
        int AttemptCount,
        Guid? LeaseToken,
        DateTime? LeaseUntil,
        int JobContentVersion,
        int JobProcessingVersion,
        DateTime? NextAttemptAt,
        string DocumentStatus,
        int DocumentContentVersion,
        int DocumentProcessingVersion,
        DateTime? DeletedAt);
}