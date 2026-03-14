# Architecture Overview

## Components

```
┌──────────────┐     HTTP      ┌──────────────┐
│   Client     │──────────────▶│     API      │
└──────────────┘               │ (ASP.NET 10) │
                               └──────┬───────┘
                                      │  Persist
                                      ▼
                               ┌──────────────┐
                               │  PostgreSQL  │
                               │  (Jobs DB)   │
                               └──────┬───────┘
                                      │  EF Core
                               ┌──────┴───────┐
                               │   RabbitMQ   │◀─────┐
                               │  (Broker)    │      │
                               └──────┬───────┘      │
                                      │ Consume       │
                                      ▼               │
                               ┌──────────────┐       │
                               │    Worker    │       │
                               │ (MassTransit │───────┘
                               │  Consumer)   │  Publish
                               └──────────────┘  JobCompleted
```

## Request Flow

1. A client sends `POST /api/jobs` with `{ jobType, payload }`.
2. The **API** creates a `JobEntity` (status = `Pending`) in **PostgreSQL** via EF Core.
3. The **API** publishes an `EnqueueJobCommand` message to **RabbitMQ** via MassTransit.
4. The **Worker** (`ImportCsvConsumer`) picks up the message.
5. The Worker performs an idempotency check (skips if already `Processing` or `Succeeded`).
6. The Worker marks the job `Processing`, executes the task, then marks `Succeeded` or `Failed`.
7. The Worker publishes a `JobCompleted` event.

## Idempotency

Before processing, the worker checks if the job is already in `Processing` or `Succeeded` state. This guards against duplicate deliveries from RabbitMQ.

## Outbox Pattern (TODO)

For production reliability, consider the [MassTransit Outbox](https://masstransit.io/documentation/configuration/persistence/outbox) to atomically persist and publish messages within a single database transaction. This prevents message loss if the broker is unreachable at publish time.

## Observability

| Signal   | Tool           | Endpoint              |
|----------|----------------|-----------------------|
| Logs     | Serilog        | stdout (structured)   |
| Traces   | OpenTelemetry  | Jaeger :16686         |
| Metrics  | prometheus-net | /metrics → Prometheus |

## Data Model

`Jobs` table (PostgreSQL):

| Column       | Type        | Description                        |
|--------------|-------------|------------------------------------|
| Id           | UUID        | Primary key                        |
| Type         | VARCHAR(100)| Job type identifier                |
| Payload      | JSONB       | Arbitrary job input                |
| Status       | VARCHAR(50) | Pending / Processing / Succeeded / Failed |
| Attempts     | INT         | Number of processing attempts      |
| MaxAttempts  | INT         | Retry ceiling                      |
| ScheduledAt  | TIMESTAMPTZ | When the job should be processed   |
| CreatedAt    | TIMESTAMPTZ | Creation timestamp                 |
| ProcessedAt  | TIMESTAMPTZ | Completion timestamp               |
| LastError    | TEXT        | Last error message if failed       |
