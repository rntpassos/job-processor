using Infrastructure.Persistence;
using Infrastructure.Repositories;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, config) =>
    config.ReadFrom.Configuration(context.Configuration)
          .Enrich.FromLogContext()
          .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Database connection string 'ConnectionStrings:Default' is not configured. " +
        "Please configure it in appsettings.json, environment variables, or another configuration source.");
}

builder.Services.AddDbContext<JobDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IJobRepository, JobRepository>();

builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((ctx, cfg) =>
    {
        var host = builder.Configuration["RABBITMQ__HOST"] ?? "localhost";
        var port = int.TryParse(builder.Configuration["RABBITMQ__PORT"], out var parsedPort) ? parsedPort : 5672;
        var user = builder.Configuration["RABBITMQ__USER"] ?? "guest";
        var pass = builder.Configuration["RABBITMQ__PASSWORD"] ?? "guest";

        var rabbitUri = new Uri($"rabbitmq://{host}:{port}/");
        cfg.Host(rabbitUri, h =>
        {
            h.Username(user);
            h.Password(pass);
        });

        cfg.ConfigureEndpoints(ctx);
    });
});

builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgres");

var otelEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://localhost:4317";
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Api"))
            .AddAspNetCoreInstrumentation()
            .AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint));
    });

var app = builder.Build();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseHttpMetrics();
app.MapControllers();
app.MapMetrics();
app.MapHealthChecks("/health/ready");
app.MapHealthChecks("/health/live");

app.Run();
