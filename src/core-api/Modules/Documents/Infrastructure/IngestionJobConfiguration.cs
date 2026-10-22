using EnterpriseGenAI.Core.Api.Modules.Documents.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseGenAI.Core.Api.Modules.Documents.Infrastructure;

public sealed class IngestionJobConfiguration : IEntityTypeConfiguration<IngestionJob>
{
    public void Configure(EntityTypeBuilder<IngestionJob> builder)
    {
        builder.ToTable("ingestion_jobs");

        builder.HasKey(j => j.JobId);

        builder.Property(j => j.DocumentId).IsRequired();
        builder.Property(j => j.ContentVersion).IsRequired();
        builder.Property(j => j.ProcessingVersion).IsRequired();

        builder.Property(j => j.ModelId).IsRequired().HasMaxLength(128);
        builder.Property(j => j.ModelRevision).IsRequired().HasMaxLength(128);

        builder.Property(j => j.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(j => j.AttemptCount).IsRequired();
        builder.Property(j => j.LastErrorCode).HasMaxLength(64);
        builder.Property(j => j.CorrelationId).HasMaxLength(128);
        builder.Property(j => j.CreatedAt).IsRequired();
        builder.Property(j => j.UpdatedAt).IsRequired();

        builder.HasOne<Document>()
            .WithMany()
            .HasForeignKey(j => j.DocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(j => new { j.DocumentId, j.ProcessingVersion }).IsUnique();
        builder.HasIndex(j => j.Status);
        builder.HasIndex(j => j.NextAttemptAt);
    }
}