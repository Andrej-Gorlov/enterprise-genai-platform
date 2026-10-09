using EnterpriseGenAI.Core.Api.Options;
using EnterpriseGenAI.Core.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using EnterpriseGenAI.Core.Api.Modules.Chats.Api;
using EnterpriseGenAI.Core.Api.Infrastructure.Http;
using EnterpriseGenAI.Core.Api.Infrastructure.Ai;
using Microsoft.Extensions.Options;
using EnterpriseGenAI.Core.Api.Modules.Ai.Api;


var migrateOnly = args is ["--migrate"];

var builder = WebApplication.CreateBuilder(migrateOnly ? [] : args);

var connectionString =
    builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("Connection string 'Database' is not configured.");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

builder.Services
    .AddOptions<ApplicationOptions>()
    .Bind(builder.Configuration.GetSection(ApplicationOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<AiOrchestratorOptions>()
    .Bind(builder.Configuration.GetSection(AiOrchestratorOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
        "AiOrchestrator:BaseUrl must be a valid HTTP or HTTPS URL.")
    .ValidateOnStart();

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<CorrelationIdPropagationHandler>();
builder.Services.AddHttpClient<IAiOrchestratorClient, AiOrchestratorClient>((serviceProvider, client) =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<AiOrchestratorOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(
options.TimeoutSeconds);
    }).AddHttpMessageHandler<CorrelationIdPropagationHandler>();

builder.Services.AddValidation();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AiServiceExceptionHandler>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Web", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (migrateOnly)
{
    try
    {
        await using var scope = app.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        app.Logger.LogInformation("Checking database migrations...");

        await dbContext.Database.MigrateAsync();

        app.Logger.LogInformation("Database migrations are up to date.");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Database migration failed.");
        Environment.ExitCode = 1;
    }

    return;
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseCors("Web");

app.MapHealthChecks("/health");
app.MapChatEndpoints();
app.MapAiEndpoints();

app.Run();

public partial class Program;