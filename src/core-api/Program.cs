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

builder.Services.AddValidation();
builder.Services.AddProblemDetails();

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

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseCors("Web");

app.MapHealthChecks("/health");
app.MapChatEndpoints();

app.Run();