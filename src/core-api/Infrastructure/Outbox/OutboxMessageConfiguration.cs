using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseGenAI.Core.Api.Infrastructure.Outbox;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.MessageId);

        builder.Property(m => m.EventType).IsRequired().HasMaxLength(128);
        builder.Property(m => m.SchemaVersion).IsRequired();
        builder.Property(m => m.Payload).IsRequired().HasColumnType("jsonb");
        builder.Property(m => m.CreatedAt).IsRequired();

        // Неопубликованные: PublishedAt IS NULL, порядок по CreatedAt
        builder.HasIndex(m => m.PublishedAt);
        builder.HasIndex(m => m.CreatedAt);
    }
}