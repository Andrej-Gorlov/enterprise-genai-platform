using EnterpriseGenAI.DocumentIngestion.Worker;
using EnterpriseGenAI.DocumentIngestion.Worker.Application;
using EnterpriseGenAI.DocumentIngestion.Worker.Messaging;
using EnterpriseGenAI.DocumentIngestion.Worker.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<RabbitMqOptions>()
    .Bind(builder.Configuration.GetSection(RabbitMqOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IIngestionMessageHandler, IngestionMessageHandler>();
builder.Services.AddSingleton<RabbitMqConsumer>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();