using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IntegrationTests;

public class JobsApiFixture : IAsyncLifetime
{
    public PostgreSqlContainer Postgres { get; } = new PostgreSqlBuilder()
        .WithImage("postgres:15")
        .WithDatabase("jobsdb")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public RabbitMqContainer RabbitMq { get; } = new RabbitMqBuilder()
        .WithImage("rabbitmq:3-management")
        .Build();

    public async Task InitializeAsync()
    {
        await Postgres.StartAsync();
        await RabbitMq.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await Postgres.DisposeAsync();
        await RabbitMq.DisposeAsync();
    }
}

public class JobsControllerTests : IClassFixture<JobsApiFixture>
{
    private readonly JobsApiFixture _fixture;

    public JobsControllerTests(JobsApiFixture fixture)
    {
        _fixture = fixture;
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        var connStr = _fixture.Postgres.GetConnectionString();
        var rabbitHost = _fixture.RabbitMq.Hostname;
        var rabbitPort = _fixture.RabbitMq.GetMappedPublicPort(5672);

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<JobDbContext>>();
                    services.RemoveAll<JobDbContext>();

                    services.AddDbContext<JobDbContext>(opts =>
                        opts.UseNpgsql(connStr));
                });

                builder.UseSetting("RABBITMQ__HOST", rabbitHost);
                builder.UseSetting("RABBITMQ__PORT", rabbitPort.ToString());
                builder.UseSetting("RABBITMQ__USER", "guest");
                builder.UseSetting("RABBITMQ__PASSWORD", "guest");
                builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317");
            });
    }

    [Fact]
    public async Task PostJob_ShouldReturn202AndPersistJob()
    {
        await using var factory = CreateFactory();

        // Ensure the schema is created
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<JobDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        var client = factory.CreateClient();

        var payload = new { jobType = "ImportCsv", payload = "{\"file\":\"data.csv\"}" };
        var response = await client.PostAsJsonAsync("/api/jobs", payload);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(body);
        Assert.True(body!.ContainsKey("jobId"));

        var jobId = Guid.Parse(body["jobId"].ToString()!);

        // Verify persisted
        using var scope2 = factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<JobDbContext>();
        var job = await db2.Jobs.FindAsync(jobId);
        Assert.NotNull(job);
        Assert.Equal("Pending", job!.Status);
    }

    [Fact]
    public async Task GetJob_WhenNotExists_ShouldReturn404()
    {
        await using var factory = CreateFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<JobDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        var client = factory.CreateClient();
        var response = await client.GetAsync($"/api/jobs/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
