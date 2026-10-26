using EnterpriseGenAI.DocumentIngestion.Worker.Application;
using EnterpriseGenAI.DocumentIngestion.Worker.Contracts;
using EnterpriseGenAI.DocumentIngestion.Worker.Infrastructure;
using EnterpriseGenAI.DocumentIngestion.Worker.Options;
using Npgsql;

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
            Assert.True(acquired.LeaseUntil > DateTime.UtcNow);

            var row = await ReadAsync(seed);
            Assert.Equal("Running", row.JobStatus);
            Assert.Equal("Processing", row.DocumentStatus);
            Assert.Equal(1, row.AttemptCount);
            Assert.Equal(acquired.LeaseToken, row.LeaseToken);
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
            Assert.Single(results, static result => result is IngestionClaimResult.NotAcquired);

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
    [InlineData(99, "Queued", "Queued")]
    [InlineData(1, "Running", "Queued")]
    [InlineData(1, "Queued", "Uploaded")]
    public async Task Stale_version_or_wrong_status_changes_nothing(int processingVersion, string jobStatus, string documentStatus)
    {
        var seed = await SeedAsync(jobStatus, documentStatus);
        try
        {
            var message = seed.Message with { ProcessingVersion = processingVersion };
            await using var dataSource = NpgsqlDataSource.Create(_connectionString);

            var result = await CreateClaimer(dataSource).TryClaimAsync(message, CancellationToken.None);

            Assert.IsType<IngestionClaimResult.NotAcquired>(result);

            var row = await ReadAsync(seed);
            Assert.Equal(jobStatus, row.JobStatus);
            Assert.Equal(documentStatus, row.DocumentStatus);
            Assert.Equal(0, row.AttemptCount);
            Assert.Null(row.LeaseToken);
        }
        finally
        {
            await DeleteAsync(seed);
        }
    }

    private static PostgresIngestionJobClaimer CreateClaimer(NpgsqlDataSource dataSource) =>
        new(dataSource, Microsoft.Extensions.Options.Options.Create(new IngestionLeaseOptions { LeaseSeconds = 120 }));

    private async Task<Seed> SeedAsync(string jobStatus, string documentStatus)
    {
        var documentId = Guid.CreateVersion7();
        var jobId = Guid.CreateVersion7();
        var now = DateTime.UtcNow;

        await using var dataSource = NpgsqlDataSource.Create(_connectionString);
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            INSERT INTO documents (
                "DocumentId", "OwnerId", "FileName", "ContentType", "Size", "TemporaryObjectKey",
                "ContentVersion", "ProcessingVersion", "Status", "UploadExpiresAtUtc", "CreatedAt", "UpdatedAt")
            VALUES (
                @documentId, 'test-owner', 'claim.txt', 'text/plain', 1, @temporaryKey,
                1, 1, @documentStatus, @expires, @now, @now);

            INSERT INTO ingestion_jobs (
                "JobId", "DocumentId", "ContentVersion", "ProcessingVersion",
                "ModelId", "ModelRevision", "Status", "AttemptCount", "CreatedAt", "UpdatedAt")
            VALUES (
                @jobId, @documentId, 1, 1,
                'test-model', 'test-revision', @jobStatus, 0, @now, @now);
            """, connection);

        command.Parameters.AddWithValue("documentId", documentId);
        command.Parameters.AddWithValue("jobId", jobId);
        command.Parameters.AddWithValue("temporaryKey", $"tmp/{documentId:N}");
        command.Parameters.AddWithValue("documentStatus", documentStatus);
        command.Parameters.AddWithValue("jobStatus", jobStatus);
        command.Parameters.AddWithValue("expires", now.AddHours(1));
        command.Parameters.AddWithValue("now", now);
        await command.ExecuteNonQueryAsync();

        return new Seed(jobId, documentId, new DocumentIngestionRequested(jobId, documentId, 1, 1));
    }

    private async Task<Row> ReadAsync(Seed seed)
    {
        await using var dataSource = NpgsqlDataSource.Create(_connectionString);
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            SELECT j."Status", j."AttemptCount", j."LeaseToken", d."Status"
            FROM ingestion_jobs j
            JOIN documents d ON d."DocumentId" = j."DocumentId"
            WHERE j."JobId" = @jobId
            """, connection);
        command.Parameters.AddWithValue("jobId", seed.JobId);

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return new Row(reader.GetString(0), reader.GetInt32(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.GetString(3));
    }

    private async Task DeleteAsync(Seed seed)
    {
        await using var dataSource = NpgsqlDataSource.Create(_connectionString);
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            DELETE FROM ingestion_jobs WHERE "JobId" = @jobId;
            DELETE FROM documents WHERE "DocumentId" = @documentId;
            """, connection);
        command.Parameters.AddWithValue("jobId", seed.JobId);
        command.Parameters.AddWithValue("documentId", seed.DocumentId);
        await command.ExecuteNonQueryAsync();
    }

    private sealed record Seed(Guid JobId, Guid DocumentId, DocumentIngestionRequested Message);

    private sealed record Row(string JobStatus, int AttemptCount, Guid? LeaseToken, string DocumentStatus);
}