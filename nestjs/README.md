# PulseEvents (NestJS)

## Overview

NestJS implementation of the same event-driven flow as the .NET stack:

- `orders-service` on `http://localhost:6101`
- `notifications-service` on `http://localhost:6102`

Infrastructure:

- RabbitMQ AMQP: `localhost:5673`
- RabbitMQ UI: `http://localhost:15673`
- Orders DB: `localhost:15433`
- Notifications DB: `localhost:15434`

## Run

From `nestjs/`:

```bash
docker compose up --build
```

## Contract

Both services use this exact event payload:

```json
{
  "eventId": "uuid",
  "type": "OrderCreated",
  "data": {
    "orderId": "uuid",
    "customerId": "string",
    "amount": 100
  },
  "occurredAt": "ISO-8601",
  "correlationId": "uuid"
}
```

Routing:

- Exchange: `pulse.events`
- Queue: `order.created`
- Routing key: `order.created`

## Reliability

- Outbox pattern in Orders: order + outbox row written in one Prisma transaction.
- Outbox publisher: polls pending rows every 2s and marks published rows only after successful publish.
- Idempotent consumer in Notifications: `processed_events(event_id)` unique key prevents duplicate effects.
- Retry behavior: consumer retries with `x-retry-count`; after max retries, message is rejected without requeue.
