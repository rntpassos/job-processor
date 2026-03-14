# Job Processor

A .NET 10 job processing system built with ASP.NET Core, MassTransit, RabbitMQ, PostgreSQL, and OpenTelemetry.

## Architecture

- **Api** – ASP.NET Core Web API (port 5000). Accepts job submissions, persists them to Postgres, and publishes to RabbitMQ.
- **Worker** – .NET Generic Host worker. Consumes jobs from RabbitMQ, processes them, and updates their status.
- **Postgres** – Persistent job store.
- **RabbitMQ** – Message broker.
- **Jaeger** – Distributed tracing (OTLP).
- **Prometheus** – Metrics scraping.

## Run Locally with Docker Compose

### Prerequisites
- Docker Desktop or Docker Engine + Compose v2

### Start all services

```bash
docker compose up --build
```

### Endpoints

| Service    | URL                                    | Description             |
|------------|----------------------------------------|-------------------------|
| API        | http://localhost:5000/swagger          | Swagger UI              |
| API        | http://localhost:5000/api/jobs         | POST – create a job     |
| API        | http://localhost:5000/api/jobs/{id}    | GET – check job status  |
| API        | http://localhost:5000/health/ready     | Readiness health check  |
| API        | http://localhost:5000/health/live      | Liveness health check   |
| API        | http://localhost:5000/metrics          | Prometheus metrics      |
| RabbitMQ   | http://localhost:15672                 | Management UI (guest/guest) |
| Jaeger     | http://localhost:16686                 | Distributed tracing UI  |
| Prometheus | http://localhost:9090                  | Metrics query UI        |

### Create a job

```bash
curl -X POST http://localhost:5000/api/jobs \
  -H "Content-Type: application/json" \
  -d '{"jobType": "ImportCsv", "payload": "{\"file\": \"data.csv\"}"}'
```

### Get job status

```bash
curl http://localhost:5000/api/jobs/{id}
```

## Observability

- **Structured logs**: Serilog writes structured, human-readable logs to stdout, suitable for collection by your log aggregator.
- **Distributed tracing**: OpenTelemetry traces exported via OTLP to Jaeger (`http://localhost:16686`).
- **Metrics**: `prometheus-net` exposes a `/metrics` endpoint scraped by Prometheus. View dashboards at `http://localhost:9090`.

## Run Tests

```bash
# Requires Docker for Testcontainers
dotnet test src/JobProcessor.slnx
```

Integration tests use Testcontainers to spin up real Postgres and RabbitMQ instances automatically.

## Project Structure

```
src/
  Api/            ASP.NET Core Web API
  Worker/         Background worker consuming RabbitMQ messages
  Common/         Shared message contracts (records)
  Infrastructure/ EF Core DbContext, entities, repositories
tests/
  Integration/    xUnit integration tests with Testcontainers
scripts/
  db/init.sql     Database initialisation SQL
monitoring/
  prometheus.yml  Prometheus scrape config
docs/
  architecture.md Architecture overview
```
