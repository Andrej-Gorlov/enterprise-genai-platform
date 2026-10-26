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
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);

        DocumentRow? document = await ReadDocumentAsync(connection, transaction, message.DocumentId, cancellationToken);
        if (document is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new IngestionClaimResult.NotFound(message.JobId);
        }

        var job = await ReadJobAsync(connection, transaction, message.JobId, cancellationToken);
        if (job is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new IngestionClaimResult.NotFound(message.JobId);
        }

        if (!IsCurrentWork(message, document, job))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new IngestionClaimResult.Stale(message.JobId);
        }

        if (Classify(message.JobId, document, job) is { } blocked)
        {
            await transaction.RollbackAsync(cancellationToken);
            return blocked;
        }

        var leaseToken = Guid.CreateVersion7();
        DateTime? leaseUntil = await TryAcquireAsync(connection, transaction, message, leaseToken, cancellationToken);
        if (leaseUntil is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new InvalidOperationException("Захват не изменил заблокированные строки в состоянии Queued.");
        }

        await transaction.CommitAsync(cancellationToken);
        return new IngestionClaimResult.Acquired(message.JobId, leaseToken, leaseUntil.Value);
    }

    private async Task<DateTime?> TryAcquireAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DocumentIngestionRequested message,
        Guid leaseToken,
        CancellationToken cancellationToken)
    {
        var leaseUntil = await ExecuteScalarTimestampAsync(connection, transaction, """
            UPDATE ingestion_jobs AS j
            SET "Status" = 'Running',
                "AttemptCount" = "AttemptCount" + 1,
                "LeaseToken" = @leaseToken,
                "LeaseUntil" = t.now + (@leaseSeconds * INTERVAL '1 second'),
                "UpdatedAt" = t.now
            FROM (SELECT clock_timestamp() AS now) AS t
            WHERE j."JobId" = @jobId
              AND j."DocumentId" = @documentId
              AND j."Status" = 'Queued'
              AND j."ContentVersion" = @contentVersion
              AND j."ProcessingVersion" = @processingVersion
            RETURNING j."LeaseUntil"
            """,
            cancellationToken,
            ("jobId", message.JobId),
            ("documentId", message.DocumentId),
            ("contentVersion", message.ContentVersion),
            ("processingVersion", message.ProcessingVersion),
            ("leaseToken", leaseToken),
            ("leaseSeconds", leaseOptions.Value.LeaseSeconds));

        if (leaseUntil is null)
            return null;

        var documentUpdated = await ExecuteAsync(connection, transaction, """
            UPDATE documents
            SET "Status" = 'Processing',
                "UpdatedAt" = clock_timestamp()
            WHERE "DocumentId" = @documentId
              AND "Status" = 'Queued'
              AND "DeletedAt" IS NULL
              AND "ContentVersion" = @contentVersion
              AND "ProcessingVersion" = @processingVersion
            """,
            cancellationToken,
            ("documentId", message.DocumentId),
            ("contentVersion", message.ContentVersion),
            ("processingVersion", message.ProcessingVersion));

        return documentUpdated == 1 ? leaseUntil : null;
    }

    // Актуальность
    private static bool IsCurrentWork(DocumentIngestionRequested message, DocumentRow document, JobRow job) =>
        job.DocumentId == message.DocumentId
        && job.ContentVersion == message.ContentVersion
        && job.ProcessingVersion == message.ProcessingVersion
        && document.ContentVersion == message.ContentVersion
        && document.ProcessingVersion == message.ProcessingVersion
        && document.DeletedAt is null;

    // null — обе строки в Queued, можно захватывать.
    // Running не перезахватывается даже с истёкшим LeaseUntil: это работа recovery.
    // RetryWait не переводится в Running ни до, ни после NextAttemptAt.
    private static IngestionClaimResult? Classify(Guid jobId, DocumentRow document, JobRow job) => job.Status switch
    {
        "Running" => new IngestionClaimResult.AlreadyRunning(jobId),
        "Succeeded" => new IngestionClaimResult.AlreadyCompleted(jobId),
        "Failed" or "Superseded" or "Cancelled" => new IngestionClaimResult.Terminal(jobId, job.Status),
        "RetryWait" => new IngestionClaimResult.RetryScheduled(jobId, job.NextAttemptAt),
        "Queued" when document.Status == "Queued" => null,
        "Queued" => new IngestionClaimResult.NotClaimable(jobId),
        _ => throw new InvalidOperationException($"Неизвестный статус задания «{job.Status}».")
    };

    private static async Task<DocumentRow?> ReadDocumentAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid documentId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT "ContentVersion", "ProcessingVersion", "Status", "DeletedAt"
            FROM documents
            WHERE "DocumentId" = @documentId
            FOR UPDATE
            """, connection, transaction);
        command.Parameters.AddWithValue("documentId", documentId);

        await using NpgsqlDataReader? reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new DocumentRow(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetDateTime(3));
    }

    private static async Task<JobRow?> ReadJobAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid jobId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT "DocumentId", "ContentVersion", "ProcessingVersion", "Status", "NextAttemptAt"
            FROM ingestion_jobs
            WHERE "JobId" = @jobId
            FOR UPDATE
            """, connection, transaction);
        command.Parameters.AddWithValue("jobId", jobId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new JobRow(
            reader.GetGuid(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : DateTime.SpecifyKind(reader.GetDateTime(4), DateTimeKind.Utc));
    }

    private static async Task<int> ExecuteAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, CancellationToken cancellationToken, params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        AddParameters(command, parameters);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<DateTime?> ExecuteScalarTimestampAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, CancellationToken cancellationToken, params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        AddParameters(command, parameters);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is DateTime timestamp ? DateTime.SpecifyKind(timestamp, DateTimeKind.Utc) : null;
    }

    private static void AddParameters(NpgsqlCommand command, params (string Name, object Value)[] parameters)
    {
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
    }

    private sealed record DocumentRow(int ContentVersion, int ProcessingVersion, string Status, DateTime? DeletedAt);

    private sealed record JobRow(Guid DocumentId, int ContentVersion, int ProcessingVersion, string Status, DateTime? NextAttemptAt);
}