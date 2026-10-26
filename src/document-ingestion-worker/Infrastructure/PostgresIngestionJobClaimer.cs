using EnterpriseGenAI.DocumentIngestion.Worker.Application;
using EnterpriseGenAI.DocumentIngestion.Worker.Contracts;
using EnterpriseGenAI.DocumentIngestion.Worker.Options;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EnterpriseGenAI.DocumentIngestion.Worker.Infrastructure;

public sealed class PostgresIngestionJobClaimer(NpgsqlDataSource dataSource, IOptions<IngestionLeaseOptions> leaseOptions) : IIngestionJobClaimer
{
    public async Task<IngestionClaimResult> TryClaimAsync(DocumentIngestionRequested message, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var document = await ReadDocumentAsync(connection, transaction, message.DocumentId, cancellationToken);
        if (document is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return IngestionClaimResult.NotAcquired.Instance;
        }

        var job = await ReadJobAsync(connection, transaction, message.JobId, cancellationToken);
        if (job is null || !CanClaim(message, document, job))
        {
            await transaction.RollbackAsync(cancellationToken);
            return IngestionClaimResult.NotAcquired.Instance;
        }

        var now = DateTime.UtcNow;
        var leaseToken = Guid.CreateVersion7();
        var leaseUntil = now.AddSeconds(leaseOptions.Value.LeaseSeconds);

        var jobUpdated = await ExecuteAsync(connection, transaction, """
            UPDATE ingestion_jobs
            SET "Status" = 'Running',
                "AttemptCount" = "AttemptCount" + 1,
                "LeaseToken" = @leaseToken,
                "LeaseUntil" = @leaseUntil,
                "UpdatedAt" = @now
            WHERE "JobId" = @jobId
              AND "Status" = 'Queued'
            """,
            cancellationToken,
            ("jobId", message.JobId),
            ("leaseToken", leaseToken),
            ("leaseUntil", leaseUntil),
            ("now", now));

        var documentUpdated = await ExecuteAsync(connection, transaction, """
            UPDATE documents
            SET "Status" = 'Processing',
                "UpdatedAt" = @now
            WHERE "DocumentId" = @documentId
              AND "Status" = 'Queued'
              AND "DeletedAt" IS NULL
            """,
            cancellationToken,
            ("documentId", message.DocumentId),
            ("now", now));

        if (jobUpdated != 1 || documentUpdated != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return IngestionClaimResult.NotAcquired.Instance;
        }

        await transaction.CommitAsync(cancellationToken);
        return new IngestionClaimResult.Acquired(message.JobId, leaseToken, leaseUntil);
    }

    private static bool CanClaim(DocumentIngestionRequested message, DocumentRow document, JobRow job) =>
        job.DocumentId == message.DocumentId
        && job.ContentVersion == message.ContentVersion
        && job.ProcessingVersion == message.ProcessingVersion
        && document.ContentVersion == message.ContentVersion
        && document.ProcessingVersion == message.ProcessingVersion
        && job.Status == "Queued"
        && document.Status == "Queued"
        && document.DeletedAt is null;

    private static async Task<DocumentRow?> ReadDocumentAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid documentId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT "ContentVersion", "ProcessingVersion", "Status", "DeletedAt"
            FROM documents
            WHERE "DocumentId" = @documentId
            FOR UPDATE
            """, connection, transaction);
        command.Parameters.AddWithValue("documentId", documentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new DocumentRow(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetDateTime(3));
    }

    private static async Task<JobRow?> ReadJobAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid jobId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT "DocumentId", "ContentVersion", "ProcessingVersion", "Status"
            FROM ingestion_jobs
            WHERE "JobId" = @jobId
            FOR UPDATE
            """, connection, transaction);
        command.Parameters.AddWithValue("jobId", jobId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new JobRow(reader.GetGuid(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetString(3));
    }

    private static async Task<int> ExecuteAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, CancellationToken cancellationToken, params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record DocumentRow(int ContentVersion, int ProcessingVersion, string Status, DateTime? DeletedAt);

    private sealed record JobRow(Guid DocumentId, int ContentVersion, int ProcessingVersion, string Status);
}