# job-processor

A .NET 10 background job processing scaffold using RabbitMQ + MassTransit, PostgreSQL, Jaeger, Prometheus, and Serilog.

## Overview

- **API** – ASP.NET Core Web API that accepts job requests and publishes them to RabbitMQ.
- **Worker** – .NET Worker Service that consumes messages, processes jobs idempotently, and updates status in PostgreSQL.
- **Infrastructure** – EF Core DbContext, entities, and repository implementations.
- **Common** – Shared message contracts (`EnqueueJobCommand`, `JobCompleted`).

## Stack

| Component | Technology |
|---|---|
| API / Worker | .NET 10, ASP.NET Core |
| Message broker | RabbitMQ 3 + MassTransit |
| Database | PostgreSQL 15 + EF Core (Npgsql) |
| Tracing | OpenTelemetry + Jaeger |
| Metrics | Prometheus (`prometheus-net`) |
| Logging | Serilog |
| Tests | xUnit + Testcontainers |
| CI | GitHub Actions |

## Running locally (Docker Compose)

```bash
docker compose up --build
```

| Service | URL |
|---|---|
| API | http://localhost:5000/swagger |
| RabbitMQ Management | http://localhost:15672 (guest/guest) |
| Jaeger UI | http://localhost:16686 |
| Prometheus | http://localhost:9090 |

## Example requests

```bash
# Create a job
curl -X POST http://localhost:5000/api/jobs \
  -H "Content-Type: application/json" \
  -d '{"type":"ImportCsv","payload":"{\"file\":\"data.csv\"}"}'

# Get job status
curl http://localhost:5000/api/jobs/{id}
```

## Running tests

```bash
dotnet test ./tests
```

## EF Core migrations

```bash
dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/Api
dotnet ef database update --project src/Infrastructure --startup-project src/Api
```

## Architecture

See [docs/architecture.md](docs/architecture.md).

## What I learned / Decisions

- Used **outbox-style idempotency**: each job has a unique ID checked before processing.
- **MassTransit** provides retry policies with exponential backoff and dead-letter queue support out of the box.
- **OpenTelemetry** instruments both HTTP and message bus traces and exports to Jaeger.
