using JobProcessor.Infrastructure.Persistence;
using JobProcessor.Infrastructure.Repositories;
using JobProcessor.Worker.Consumers;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSerilog((ctx, cfg) =>
        cfg.ReadFrom.Configuration(builder.Configuration)
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
        x.AddConsumer<ImportCsvConsumer>();

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

            cfg.ReceiveEndpoint("enqueue-job", e =>
            {
                e.UseMessageRetry(r => r.Exponential(5,
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromSeconds(2)));
                e.ConfigureConsumer<ImportCsvConsumer>(ctx);
            });
        });
    });

    // Prometheus stand-alone metrics server on port 9151
    var metricsServer = new MetricServer(port: 9151);
    metricsServer.Start();

    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Worker terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
