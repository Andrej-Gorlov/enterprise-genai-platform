using EnterpriseGenAI.DocumentIngestion.Worker.Contracts;
using EnterpriseGenAI.DocumentIngestion.Worker.Infrastructure;
using EnterpriseGenAI.DocumentIngestion.Worker.Options;
using Npgsql;
using NpgsqlTypes;

namespace EnterpriseGenAI.DocumentIngestion.Worker.Tests;

public sealed class IngestionLeaseTests
{
    private readonly string _connectionString =
        Environment.GetEnvironmentVariable("INGESTION_TEST_CONNECTION") ?? throw new InvalidOperationException("Set INGESTION_TEST_CONNECTION.");

    [Fact]
    public async Task Matching_token_renews_only_lease_deadline()
    {
        var token = Guid.CreateVersion7();
        var seed = await SeedAsync(token, leaseOffsetSeconds: 15, attemptCount: 2);
        try
        {
            var before = await ReadAsync(seed);
            await using var dataSource = NpgsqlDataSource.Create(_connectionString);

            var renewed = await CreateManager(dataSource).TryRenewAsync(seed.Message, token, CancellationToken.None);

            Assert.NotNull(renewed);
            var after = await ReadAsync(seed);
            Assert.Equal(renewed, after.LeaseUntil);
            Assert.True(after.LeaseUntil > before.LeaseUntil);
            Assert.Equal(before with { LeaseUntil = after.LeaseUntil, UpdatedAt = after.UpdatedAt }, after);
            Assert.NotEqual(before.UpdatedAt, after.UpdatedAt);
            Assert.Equal(token, after.LeaseToken);
            Assert.Equal(2, after.AttemptCount);
            Assert.Equal("Running", after.JobStatus);
            Assert.Equal("Processing", after.DocumentStatus);

            var remaining = await LeaseRemainingAsync(seed.JobId);
            Assert.InRange(remaining, TimeSpan.FromSeconds(100), TimeSpan.FromSeconds(120));
        }
        finally
        {
            await DeleteAsync(seed);
        }
    }

    [Fact]
    public async Task Wrong_token_does_not_change_the_row()
    {
        var token = Guid.CreateVersion7();
        var seed = await SeedAsync(token, leaseOffsetSeconds: 40, attemptCount: 2);
        try
        {
            var before = await ReadAsync(seed);
            await using var dataSource = NpgsqlDataSource.Create(_connectionString);

            var renewed = await CreateManager(dataSource).TryRenewAsync(seed.Message, Guid.CreateVersion7(), CancellationToken.None);

            Assert.Null(renewed);
            Assert.Equal(before, await ReadAsync(seed));
        }
        finally
        {
            await DeleteAsync(seed);
        }
    }

    [Fact]
    public async Task Expired_lease_is_not_renewed_with_the_same_token()
    {
        var token = Guid.CreateVersion7();
        var seed = await SeedAsync(token, leaseOffsetSeconds: -30, attemptCount: 2);
        try
        {
            var before = await ReadAsync(seed);
            await using var dataSource = NpgsqlDataSource.Create(_connectionString);

            var renewed = await CreateManager(dataSource).TryRenewAsync(seed.Message, token, CancellationToken.None);

            Assert.Null(renewed);
            Assert.Equal(before, await ReadAsync(seed));
        }
        finally
        {
            await DeleteAsync(seed);
        }
    }

    [Fact]
    public async Task Superseded_token_cannot_change_the_replacement_lease()
    {
        var tokenA = Guid.CreateVersion7();
        var tokenB = Guid.CreateVersion7();
        var seed = await SeedAsync(tokenA, leaseOffsetSeconds: 20, attemptCount: 4);
        try
        {
            await ExecuteAsync("""
                UPDATE ingestion_jobs
                SET "LeaseToken" = @tokenB,
                    "LeaseUntil" = clock_timestamp() + INTERVAL '10 minutes',
                    "UpdatedAt" = clock_timestamp()
                WHERE "JobId" = @jobId
                  AND "LeaseToken" = @tokenA
                """,
                ("jobId", seed.JobId),
                ("tokenA", tokenA),
                ("tokenB", tokenB));

            var before = await ReadAsync(seed);
            Assert.Equal(tokenB, before.LeaseToken);

            await using var dataSource = NpgsqlDataSource.Create(_connectionString);
            var renewed = await CreateManager(dataSource).TryRenewAsync(seed.Message, tokenA, CancellationToken.None);

            Assert.Null(renewed);
            Assert.Equal(before, await ReadAsync(seed));
        }
        finally
        {
            await DeleteAsync(seed);
        }
    }

    [Theory]
    [InlineData("version")]
    [InlineData("delete")]
    [InlineData("job-version")]
    [InlineData("status")]
    public async Task Version_change_or_deletion_rejects_renewal(string change)
    {
        var token = Guid.CreateVersion7();
        var seed = await SeedAsync(token, leaseOffsetSeconds: 50, attemptCount: 1);
        try
        {
            var sql = change switch
            {
                "version" => """
                    UPDATE documents
                    SET "ProcessingVersion" = "ProcessingVersion" + 1,
                        "UpdatedAt" = clock_timestamp()
                    WHERE "DocumentId" = @documentId
                    """,
                "delete" => """
                    UPDATE documents
                    SET "DeletedAt" = clock_timestamp(),
                        "UpdatedAt" = clock_timestamp()
                    WHERE "DocumentId" = @documentId
                    """,
                "job-version" => """
                    UPDATE ingestion_jobs
                    SET "ContentVersion" = "ContentVersion" + 1,
                        "UpdatedAt" = clock_timestamp()
                    WHERE "JobId" = @jobId
                    """,
                "status" => """
                    UPDATE documents
                    SET "Status" = 'Ready',
                        "UpdatedAt" = clock_timestamp()
                    WHERE "DocumentId" = @documentId
                    """,
                _ => throw new ArgumentOutOfRangeException(nameof(change))
            };

            await ExecuteAsync(sql, ("documentId", seed.DocumentId), ("jobId", seed.JobId));
            var before = await ReadAsync(seed);
            await using var dataSource = NpgsqlDataSource.Create(_connectionString);

            var renewed = await CreateManager(dataSource).TryRenewAsync(seed.Message, token, CancellationToken.None);

            Assert.Null(renewed);
            Assert.Equal(before, await ReadAsync(seed));
        }
        finally
        {
            await DeleteAsync(seed);
        }
    }

    [Fact]
    public async Task Database_outage_throws()
    {
        await using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=none;Password=none;Database=none;Timeout=2;Command Timeout=2");
        var manager = CreateManager(dataSource);
        var message = new DocumentIngestionRequested(Guid.CreateVersion7(), Guid.CreateVersion7(), 1, 1);

        await Assert.ThrowsAnyAsync<NpgsqlException>(() => manager.TryRenewAsync(message, Guid.CreateVersion7(), CancellationToken.None));
    }

    private static PostgresIngestionLeaseManager CreateManager(NpgsqlDataSource dataSource) =>
        new(dataSource, Microsoft.Extensions.Options.Options.Create(new IngestionLeaseOptions { LeaseSeconds = 120 }));

    private async Task<Seed> SeedAsync(Guid leaseToken, int leaseOffsetSeconds, int attemptCount)
    {
        var documentId = Guid.CreateVersion7();
        var jobId = Guid.CreateVersion7();

        await ExecuteAsync("""
            INSERT INTO documents (
                "DocumentId", "OwnerId", "FileName", "ContentType", "Size", "TemporaryObjectKey",
                "ContentVersion", "ProcessingVersion", "Status", "UploadExpiresAtUtc", "CreatedAt", "UpdatedAt")
            VALUES (
                @documentId, 'test-owner', 'lease.txt', 'text/plain', 1, @temporaryKey,
                1, 1, 'Processing', clock_timestamp() + INTERVAL '1 hour', clock_timestamp(), clock_timestamp());

            INSERT INTO ingestion_jobs (
                "JobId", "DocumentId", "ContentVersion", "ProcessingVersion",
                "ModelId", "ModelRevision", "Status", "AttemptCount", "LeaseToken", "LeaseUntil", "CreatedAt", "UpdatedAt")
            VALUES (
                @jobId, @documentId, 1, 1,
                'test-model', 'test-revision', 'Running', @attemptCount, @leaseToken,
                clock_timestamp() + (@leaseOffset * INTERVAL '1 second'),
                clock_timestamp(), clock_timestamp());
            """,
            ("documentId", documentId),
            ("jobId", jobId),
            ("temporaryKey", $"tmp/{documentId:N}"),
            ("attemptCount", attemptCount),
            ("leaseToken", leaseToken),
            ("leaseOffset", leaseOffsetSeconds));

        return new Seed(jobId, documentId, new DocumentIngestionRequested(jobId, documentId, 1, 1));
    }

    private async Task<Row> ReadAsync(Seed seed)
    {
        await using var dataSource = NpgsqlDataSource.Create(_connectionString);
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            SELECT j."Status", j."AttemptCount", j."LeaseToken", j."LeaseUntil", j."UpdatedAt",
                   j."ContentVersion", j."ProcessingVersion",
                   d."Status", d."ContentVersion", d."ProcessingVersion", d."DeletedAt", d."UpdatedAt"
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
            ReadUtc(reader, 4),
            reader.GetInt32(5),
            reader.GetInt32(6),
            reader.GetString(7),
            reader.GetInt32(8),
            reader.GetInt32(9),
            ReadUtc(reader, 10),
            ReadUtc(reader, 11));
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
        return Assert.IsType<TimeSpan>(await command.ExecuteScalarAsync());
    }

    private async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var dataSource = NpgsqlDataSource.Create(_connectionString);
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            var parameter = value switch
            {
                Guid guid => new NpgsqlParameter(name, NpgsqlDbType.Uuid) { Value = guid },
                int number => new NpgsqlParameter(name, NpgsqlDbType.Integer) { Value = number },
                _ => new NpgsqlParameter(name, value)
            };
            command.Parameters.Add(parameter);
        }

        await command.ExecuteNonQueryAsync();
    }

    private async Task DeleteAsync(Seed seed)
    {
        await ExecuteAsync("""
            DELETE FROM ingestion_jobs WHERE "JobId" = @jobId OR "DocumentId" = @documentId;
            DELETE FROM documents WHERE "DocumentId" = @documentId;
            """,
            ("jobId", seed.JobId),
            ("documentId", seed.DocumentId));
    }

    private static DateTime? ReadUtc(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : DateTime.SpecifyKind(reader.GetDateTime(ordinal), DateTimeKind.Utc);

    private sealed record Seed(Guid JobId, Guid DocumentId, DocumentIngestionRequested Message);

    private sealed record Row(
        string JobStatus,
        int AttemptCount,
        Guid? LeaseToken,
        DateTime? LeaseUntil,
        DateTime? UpdatedAt,
        int JobContentVersion,
        int JobProcessingVersion,
        string DocumentStatus,
        int DocumentContentVersion,
        int DocumentProcessingVersion,
        DateTime? DeletedAt,
        DateTime? DocumentUpdatedAt);
}