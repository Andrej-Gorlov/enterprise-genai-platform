using EnterpriseGenAI.Core.Api.Modules.Chats.Domain;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseGenAI.Core.Api.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Chat> Chats => Set<Chat>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}