using Infrastructure.Persistence;
using Infrastructure.Repositories;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Worker.Consumers;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, config) =>
    config.ReadFrom.Configuration(builder.Configuration)
          .Enrich.FromLogContext()
          .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"));

builder.Services.AddDbContext<JobDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IJobRepository, JobRepository>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ImportCsvConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        var host = builder.Configuration["RABBITMQ__HOST"] ?? "localhost";
        var user = builder.Configuration["RABBITMQ__USER"] ?? "guest";
        var pass = builder.Configuration["RABBITMQ__PASSWORD"] ?? "guest";

        cfg.Host(host, "/", h =>
        {
            h.Username(user);
            h.Password(pass);
        });

        cfg.ConfigureEndpoints(ctx);
    });
});

var otelEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://localhost:4317";
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Worker"))
            .AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint));
    });

var host = builder.Build();
host.Run();
