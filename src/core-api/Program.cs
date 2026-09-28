using EnterpriseGenAI.Core.Api.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

builder.Services
    .AddOptions<ApplicationOptions>()
    .Bind(builder.Configuration.GetSection(ApplicationOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();