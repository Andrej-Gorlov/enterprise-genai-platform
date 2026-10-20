
using EnterpriseGenAI.Core.Api.Modules.Documents.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseGenAI.Core.Api.Modules.Documents.Infrastructure;

public sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents");

        builder.HasKey(d => d.DocumentId);

        builder.Property(d => d.OwnerId)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(d => d.FileName)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(d => d.ContentType)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(d => d.Size)
            .IsRequired();

        builder.Property(d => d.TemporaryObjectKey)
            .IsRequired()
            .HasMaxLength(1024);

        builder.Property(d => d.ObjectKey)
            .HasMaxLength(1024);

        builder.Property(d => d.Checksum)
            .HasMaxLength(64);

        builder.Property(d => d.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsConcurrencyToken();

        builder.Property(d => d.UploadExpiresAtUtc)
            .IsRequired();

        builder.Property(d => d.ContentVersion)
            .IsRequired();

        builder.Property(d => d.ProcessingVersion)
            .IsRequired();

        builder.HasIndex(d => d.TemporaryObjectKey)
            .IsUnique();

        builder.HasIndex(d => d.ObjectKey)
            .IsUnique();

        builder.HasIndex(d => d.OwnerId);
        builder.HasIndex(d => d.Status);
    }
}
