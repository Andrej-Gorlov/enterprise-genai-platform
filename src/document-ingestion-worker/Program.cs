using EnterpriseGenAI.DocumentIngestion.Worker;
using EnterpriseGenAI.DocumentIngestion.Worker.Application;
using EnterpriseGenAI.DocumentIngestion.Worker.Infrastructure;
using EnterpriseGenAI.DocumentIngestion.Worker.Messaging;
using EnterpriseGenAI.DocumentIngestion.Worker.Options;
using Npgsql;

var builder = Host.CreateApplicationBuilder(args);


var connectionString = builder.Configuration.GetConnectionString("Database");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Строка подключения «Database» не настроена.");
}

builder.Services.AddSingleton<NpgsqlDataSource>(_ => NpgsqlDataSource.Create(connectionString));

builder.Services
    .AddOptions<RabbitMqOptions>()
    .Bind(builder.Configuration.GetSection(RabbitMqOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<IngestionLeaseOptions>()
    .Bind(builder.Configuration.GetSection(IngestionLeaseOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IIngestionJobClaimer, PostgresIngestionJobClaimer>();
builder.Services.AddSingleton<IIngestionLeaseManager, PostgresIngestionLeaseManager>();
builder.Services.AddSingleton<IIngestionMessageHandler, IngestionMessageHandler>();
builder.Services.AddSingleton<RabbitMqConsumer>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();