using EnterpriseGenAI.Core.Api.Infrastructure.Outbox;
using EnterpriseGenAI.Core.Api.Modules.Chats.Domain;
using EnterpriseGenAI.Core.Api.Modules.Documents.Domain;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseGenAI.Core.Api.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Chat> Chats => Set<Chat>();
    public DbSet<Document> Documents => Set<Document>();

    public DbSet<IngestionJob> IngestionJobs => Set<IngestionJob>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}