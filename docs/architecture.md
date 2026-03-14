# Architecture

## Overview

```
 ┌──────────────────────────────────────────────────────────────────┐
 │  Client (HTTP)                                                   │
 └───────────────────────────┬──────────────────────────────────────┘
                             │ POST /api/jobs
                             ▼
 ┌─────────────────────────────────────────────────────────────────┐
 │  API  (ASP.NET Core · port 5000)                                │
 │  ┌──────────────┐   ┌───────────────────────────────────────┐  │
 │  │ JobsController│──▶│ IJobRepository.AddJobAsync            │  │
 │  │              │   │  (Postgres – jobs table)              │  │
 │  │              │──▶│ IPublishEndpoint.Publish               │  │
 │  │              │   │  (EnqueueJobCommand → RabbitMQ)       │  │
 │  └──────────────┘   └───────────────────────────────────────┘  │
 └──────────────────────────────────┬──────────────────────────────┘
                                    │ AMQP
                                    ▼
 ┌──────────────────────────────────────────────────────────────────┐
 │  RabbitMQ  (port 5672/15672)                                     │
 │  exchange: enqueue-job                                           │
 └───────────────────────┬──────────────────────────────────────────┘
                         │ consume
                         ▼
 ┌──────────────────────────────────────────────────────────────────┐
 │  Worker  (.NET Worker Service)                                   │
 │  ┌────────────────────┐  ┌───────────────────────────────────┐  │
 │  │ ImportCsvConsumer  │──▶ IJobRepository.MarkProcessingAsync │  │
 │  │ (idempotent)       │  │  … do work …                      │  │
 │  │                    │──▶ MarkSucceededAsync                 │  │
 │  │                    │──▶ Publish(JobCompleted)              │  │
 │  └────────────────────┘  └───────────────────────────────────┘  │
 └──────────────────────────────────────────────────────────────────┘
                         │
                         ▼
 ┌──────────────────────────────────────────────────────────────────┐
 │  PostgreSQL 15  (port 5432)                                      │
 │  Table: jobs  (id, type, payload, status, attempts, …)          │
 └──────────────────────────────────────────────────────────────────┘
```

## Observability

```
 API / Worker
    │  traces (OTLP/Jaeger exporter)
    ▼
 Jaeger  (port 16686)

    │  /metrics (prometheus-net)
    ▼
 Prometheus  (port 9090)
```

## Key Patterns

### Idempotency
Each consumer checks `IJobRepository.IsProcessedAsync(jobId)` before performing any work.
If the job already has status `Succeeded`, `Failed`, or `DeadLetter`, the message is acknowledged and dropped.

### At-least-once delivery & Retries
MassTransit is configured with exponential retry on the `enqueue-job` receive endpoint.
After `max_attempts` failures the job moves to `DeadLetter` status and MassTransit moves the
message to the error queue for manual inspection.

### Outbox (future)
A transactional outbox can be added using `MassTransit.EntityFrameworkCore` to guarantee
that the job row and the published message are always written atomically to Postgres.

## Database Schema

See `src/Infrastructure/scripts/db/init.sql` for the `jobs` table DDL.
EF Core migrations live in `src/Infrastructure/Migrations/` (generate with `dotnet ef migrations add`).
