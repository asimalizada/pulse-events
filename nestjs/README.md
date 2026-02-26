# Pulse Events - NestJS

## Tech Stack

- NestJS 10
- Prisma ORM
- PostgreSQL
- RabbitMQ
- Docker Compose

## Architecture (ASCII)

```text
POST /orders
   |
   v
orders-service
   |  tx: orders + outbox_events
   v
outbox.publisher (2s polling)
   |
   v
exchange pulse.events -> routing key order.created -> queue order.created
   |
   v
notifications consumer
   |  idempotency: processed_events(event_id unique)
   v
notifications table
```

## Ports

- Orders API: `6101 -> 5001`
- Notifications API: `6102 -> 5002`
- RabbitMQ AMQP: `5673 -> 5672`
- RabbitMQ UI: `15673 -> 15672`
- Orders DB: `15433 -> 5432`
- Notifications DB: `15434 -> 5432`

## Run

From `nestjs/`:

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
- `outbox_events(id, event_id, type, payload, status, created_at, published_at)`
- Index: `outbox_events(status, created_at)`
- Unique: `outbox_events(event_id)`

Notifications DB:

- `notifications(id, order_id, message, created_at)`
- `processed_events(id, event_id, processed_at)`
- Unique: `processed_events(event_id)`

## Architecture Decisions

1. Event contract is shared and explicit:
   `eventId`, `type`, `data{orderId,customerId,amount}`, `occurredAt`, `correlationId`.
2. Outbox writes in the same transaction as domain data to prevent lost events.
3. Consumer is idempotent using a unique processed-events key.
4. Consumer retry is bounded with `x-retry-count` to avoid poison-message loops.
5. Health endpoints validate DB and RabbitMQ dependencies, not just process uptime.

## Troubleshooting

1. Services fail at startup with DB errors:
   Confirm `postgres_orders` and `postgres_notifications` are healthy in `docker compose ps`.
2. Events not flowing:
   Check queue/exchange in RabbitMQ UI `http://localhost:15673` (`guest/guest`).
3. Notifications missing:
   Inspect orders outbox status; `Pending` means broker path is unavailable.
4. Prisma/OpenSSL issues in Docker:
   Dockerfiles install OpenSSL explicitly; rebuild with `docker compose build --no-cache` if needed.
5. Port bind errors:
   Stop the other implementation stack before starting this one.
