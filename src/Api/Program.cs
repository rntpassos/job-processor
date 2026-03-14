using JobProcessor.Infrastructure.Persistence;
using JobProcessor.Infrastructure.Repositories;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using Serilog;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Serilog
    builder.Host.UseSerilog((ctx, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .WriteTo.Console());

    // EF Core + PostgreSQL
    builder.Services.AddDbContext<JobDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings__Default is required.")));

    // Repositories
    builder.Services.AddScoped<IJobRepository, JobRepository>();

    // MassTransit + RabbitMQ
    builder.Services.AddMassTransit(x =>
    {
        x.UsingRabbitMq((ctx, cfg) =>
        {
            var host = builder.Configuration["RABBITMQ__HOST"] ?? "localhost";
            var user = builder.Configuration["RABBITMQ__USER"] ?? "guest";
            var pass = builder.Configuration["RABBITMQ__PASSWORD"] ?? "guest";

            cfg.Host(host, h =>
            {
                h.Username(user);
                h.Password(pass);
            });

            cfg.ConfigureEndpoints(ctx);
        });
    });

    // OpenTelemetry tracing
    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing =>
        {
            tracing
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("JobProcessor.Api"))
                .AddAspNetCoreInstrumentation()
                .AddJaegerExporter(j =>
                {
                    j.AgentHost = builder.Configuration["JAEGER__HOST"] ?? "localhost";
                    j.AgentPort = int.Parse(builder.Configuration["JAEGER__PORT"] ?? "6831");
                });
        });

    // Health checks
    builder.Services.AddHealthChecks()
        .AddNpgSql(builder.Configuration.GetConnectionString("Default") ?? string.Empty, name: "postgres");

    // Swagger / controllers
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseSwagger();
    app.UseSwaggerUI();

    // Prometheus metrics endpoint
    app.UseHttpMetrics();
    app.MapMetrics();

    app.MapControllers();
    app.MapHealthChecks("/health");

    // Auto-migrate on startup
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<JobDbContext>();
        db.Database.Migrate();
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
