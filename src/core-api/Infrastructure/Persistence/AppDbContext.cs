using Microsoft.EntityFrameworkCore;

namespace EnterpriseGenAI.Core.Api.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
}