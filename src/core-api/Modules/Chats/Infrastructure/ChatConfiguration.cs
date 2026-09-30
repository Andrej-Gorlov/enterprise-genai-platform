using EnterpriseGenAI.Core.Api.Modules.Chats.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseGenAI.Core.Api.Modules.Chats.Infrastructure;

public sealed class ChatConfiguration : IEntityTypeConfiguration<Chat>
{
    public void Configure(EntityTypeBuilder<Chat> builder)
    {
        builder.ToTable("chats");

        builder.HasKey(chat => chat.Id);

        builder.Property(chat => chat.Title).HasMaxLength(200).IsRequired();

        builder.Property(chat => chat.CreatedAt).IsRequired();
    }
}