# Pulse Events (.NET)

## Overview

This folder contains two .NET microservices:

- `OrdersService` (`http://localhost:6201`)
- `NotificationsService` (`http://localhost:6202`)

Both services are event-driven over RabbitMQ and use separate PostgreSQL databases:

- `orders_db` on host port `15433`
- `notifications_db` on host port `15434`

RabbitMQ:

- AMQP: `localhost:5673`
- Management UI: `http://localhost:15673`

## Architecture (Text Diagram)

```text
Client
  |
  | POST /orders
  v
OrdersService.Api
  |-- writes orders row
  |-- writes outbox_messages row
  |   (same EF Core transaction)
  |
  | OutboxPublisherHostedService (2s polling)
  v
Exchange: pulse.events
Routing Key: order.created
Queue: order.created
  |
  v
NotificationsService consumer
  |-- idempotency gate: processed_events(event_id unique)
  |-- notifications insert
  v
GET /notifications?orderId=<guid>
```

## Run

From `dotnet/`:

```bash
docker compose up --build
```

## Test With curl

### Linux/macOS

```bash
ORDER_RESPONSE=$(curl -s -X POST http://localhost:6201/orders \
  -H "Content-Type: application/json" \
  -H "x-correlation-id: 00000000-0000-0000-0000-000000000123" \
  -d '{"customerId":"123","amount":100}')
echo "$ORDER_RESPONSE"

ORDER_ID=$(echo "$ORDER_RESPONSE" | sed -E 's/.*"orderId":"([^"]+)".*/\1/')
sleep 4
curl -s "http://localhost:6202/notifications?orderId=${ORDER_ID}"
```

### Windows (PowerShell)

```powershell
$body = @{ customerId = '123'; amount = 100 } | ConvertTo-Json -Compress
$order = Invoke-RestMethod -Method Post -Uri 'http://localhost:6201/orders' `
  -Headers @{ 'x-correlation-id' = '00000000-0000-0000-0000-000000000123' } `
  -ContentType 'application/json' -Body $body
$order

Start-Sleep -Seconds 4
Invoke-RestMethod -Method Get -Uri "http://localhost:6202/notifications?orderId=$($order.orderId)"
```

Health:

```bash
curl -s http://localhost:6201/health
curl -s http://localhost:6202/health
```

## Event Contract

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

## Reliability Notes

- Outbox pattern: Order + outbox message are committed in one transaction.
- Publisher worker: polls pending rows every 2s, publishes with retry/backoff, marks `Pending -> Published` only after successful publish.
- Idempotent consumer: `processed_events(event_id)` unique index prevents duplicates under at-least-once delivery.
- Poison message handling: failed consumer processing is retried with header `x-retry-count`; after max retries, message is rejected without requeue.
