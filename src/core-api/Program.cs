using EnterpriseGenAI.Core.Api.Options;
using EnterpriseGenAI.Core.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using EnterpriseGenAI.Core.Api.Modules.Chats.Api;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("Connection string 'Database' is not configured.");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddHealthChecks();

builder.Services
    .AddOptions<ApplicationOptions>()
    .Bind(builder.Configuration.GetSection(ApplicationOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var app = builder.Build();

app.MapHealthChecks("/health");
app.MapChatEndpoints();

app.Run();