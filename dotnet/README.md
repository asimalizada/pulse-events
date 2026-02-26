# Pulse Events - .NET

## Tech Stack

- .NET 10 (ASP.NET Core Web API)
- EF Core + Npgsql
- PostgreSQL
- RabbitMQ.Client
- Serilog (structured JSON logging)
- Docker Compose

## Architecture (ASCII)

```text
POST /orders
   |
   v
OrdersService.Api
   |  tx: orders + outbox_messages
   v
OutboxPublisherHostedService (poll 2s, retry/backoff)
   |
   v
exchange pulse.events -> routing key order.created -> queue order.created
   |
   v
OrderCreatedConsumerHostedService
   |  idempotency: processed_events(event_id unique)
   v
NotificationsService.Api (query notifications by orderId)
```

## Ports

- Orders API: `6201 -> 8080`
- Notifications API: `6202 -> 8080`
- RabbitMQ AMQP: `5673 -> 5672`
- RabbitMQ UI: `15673 -> 15672`
- Orders DB: `15433 -> 5432`
- Notifications DB: `15434 -> 5432`

## Run

From `dotnet/`:

```bash
docker compose up --build
```

Stop:

```bash
docker compose down
```

## Database Schema Summary

Orders DB:

- `orders(id, customer_id, amount, created_at)`
- `outbox_messages(id, event_id, type, payload, status, created_at, published_at)`
- Index: `outbox_messages(status, created_at)`
- Unique: `outbox_messages(event_id)`

Notifications DB:

- `notifications(id, order_id, message, created_at)`
- `processed_events(id, event_id, processed_at)`
- Unique: `processed_events(event_id)`

## Architecture Decisions

1. Orders API does not publish directly; it persists intent via outbox in the same transaction as order data.
2. Outbox worker publishes only committed `Pending` rows and marks `Published` after publish success.
3. Consumer applies idempotency before side effects to tolerate at-least-once delivery.
4. Consumer retries are bounded (`x-retry-count`) to avoid infinite poison-message requeue loops.
5. Correlation IDs are propagated via HTTP header -> event payload/header -> producer/consumer logs.
6. `/health` checks verify both DB and RabbitMQ dependencies.

## Troubleshooting

1. Notifications service exits on startup:
   Verify RabbitMQ is healthy and reachable; consumer startup retries are enabled.
2. Health endpoint not healthy:
   Check connection strings and broker credentials in compose environment.
3. Event published but no notification:
   Inspect `processed_events` for duplicate suppression and outbox status in Orders DB.
4. Port conflicts:
   Stop the NestJS stack first; both implementations use the same shared host infra ports.
5. Migration issues:
   Development startup applies migrations automatically; manual commands are in [`MIGRATIONS.md`](MIGRATIONS.md).
