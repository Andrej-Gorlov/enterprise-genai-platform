using EnterpriseGenAI.DocumentIngestion.Worker.Application;
using EnterpriseGenAI.DocumentIngestion.Worker.Contracts;
using EnterpriseGenAI.DocumentIngestion.Worker.Options;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EnterpriseGenAI.DocumentIngestion.Worker.Infrastructure;

public sealed class PostgresIngestionLeaseManager(NpgsqlDataSource dataSource, IOptions<IngestionLeaseOptions> leaseOptions) : IIngestionLeaseManager
{
    public async Task<DateTime?> TryRenewAsync(DocumentIngestionRequested message, Guid leaseToken, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // Тот же порядок, что у захвата: документ, затем задание. Иначе возможен deadlock.
        if (!await LockAsync(connection, transaction, """
            SELECT 1
            FROM documents
            WHERE "DocumentId" = @documentId
            FOR UPDATE
            """, cancellationToken, ("documentId", message.DocumentId)) || !await LockAsync(connection, transaction, """
            SELECT 1
            FROM ingestion_jobs
            WHERE "JobId" = @jobId
            FOR UPDATE
            """, cancellationToken, ("jobId", message.JobId)))
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var leaseUntil = await RenewAsync(connection, transaction, message, leaseToken, cancellationToken);
        if (leaseUntil is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        await transaction.CommitAsync(cancellationToken);
        return leaseUntil;
    }

    private async Task<DateTime?> RenewAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DocumentIngestionRequested message,
        Guid leaseToken,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            UPDATE ingestion_jobs AS j
            SET "LeaseUntil" = t.now + (@leaseSeconds * INTERVAL '1 second'),
                "UpdatedAt" = t.now
            FROM (SELECT clock_timestamp() AS now) AS t
            WHERE j."JobId" = @jobId
              AND j."DocumentId" = @documentId
              AND j."Status" = 'Running'
              AND j."LeaseToken" = @leaseToken
              AND j."LeaseUntil" > t.now
              AND j."ContentVersion" = @contentVersion
              AND j."ProcessingVersion" = @processingVersion
              AND EXISTS (
                  SELECT 1
                  FROM documents AS d
                  WHERE d."DocumentId" = j."DocumentId"
                    AND d."Status" = 'Processing'
                    AND d."DeletedAt" IS NULL
                    AND d."ContentVersion" = @contentVersion
                    AND d."ProcessingVersion" = @processingVersion)
            RETURNING j."LeaseUntil"
            """, connection, transaction);

        command.Parameters.AddWithValue("jobId", message.JobId);
        command.Parameters.AddWithValue("documentId", message.DocumentId);
        command.Parameters.AddWithValue("leaseToken", leaseToken);
        command.Parameters.AddWithValue("contentVersion", message.ContentVersion);
        command.Parameters.AddWithValue("processingVersion", message.ProcessingVersion);
        command.Parameters.AddWithValue("leaseSeconds", leaseOptions.Value.LeaseSeconds);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is DateTime timestamp ? DateTime.SpecifyKind(timestamp, DateTimeKind.Utc) : null;
    }

    private static async Task<bool> LockAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, CancellationToken cancellationToken, params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);

        return await command.ExecuteScalarAsync(cancellationToken) is not null and not DBNull;
    }
}