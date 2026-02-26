# Pulse Events (.NET)

## Overview

This folder contains two independent .NET microservices:

- `OrdersService` (port `6001`)
- `NotificationsService` (port `6002`)

They communicate asynchronously through RabbitMQ.

Each service has its own PostgreSQL database:

- `orders_db` for Orders
- `notifications_db` for Notifications

## Architecture Diagram (Text)

```text
Client
  |
  | POST /orders
  v
orders-service (API + EF Core)
  |-- writes order -> orders_db.orders
  |-- writes outbox -> orders_db.outbox_messages
  |
  | (background outbox publisher)
  v
RabbitMQ exchange: pulse.events
  |
  | routing key: order.created
  v
RabbitMQ queue: order.created
  |
  | (background consumer with manual ack)
  v
notifications-service
  |-- idempotency check/insert -> notifications_db.processed_events
  |-- insert notification      -> notifications_db.notifications
  v
GET /notifications?orderId=<guid>
```

## Run

From this `dotnet/` directory:

```bash
docker compose up --build
```

## Test With curl

### Linux/macOS (bash)

Create an order:

```bash
ORDER_RESPONSE=$(curl -s -X POST http://localhost:6001/orders \
  -H "Content-Type: application/json" \
  -d '{"customerId":"123","amount":100}')
echo "$ORDER_RESPONSE"
```

Extract `orderId`, wait briefly, then query notifications:

```bash
ORDER_ID=$(echo "$ORDER_RESPONSE" | sed -E 's/.*"orderId":"([^"]+)".*/\1/')
sleep 3
curl -s "http://localhost:6002/notifications?orderId=${ORDER_ID}"
```

Health checks:

```bash
curl -s http://localhost:6001/health
curl -s http://localhost:6002/health
```

### Windows (PowerShell)

Create an order:

```powershell
$orderResponse = curl.exe -s -X POST "http://localhost:6001/orders" `
  -H "Content-Type: application/json" `
  -d "{\"customerId\":\"123\",\"amount\":100}" | ConvertFrom-Json
$orderResponse
```

Wait briefly, then query notifications:

```powershell
Start-Sleep -Seconds 3
curl.exe -s "http://localhost:6002/notifications?orderId=$($orderResponse.orderId)"
```

Health checks:

```powershell
curl.exe -s "http://localhost:6001/health"
curl.exe -s "http://localhost:6002/health"
```

## RabbitMQ UI

- URL: `http://localhost:15672`
- Username: `guest`
- Password: `guest`

## Event Schema

`OrderCreated` event payload (stored in outbox and published to RabbitMQ):

```json
{
  "eventId": "11111111-1111-1111-1111-111111111111",
  "type": "OrderCreated",
  "data": {
    "orderId": "22222222-2222-2222-2222-222222222222",
    "customerId": "123",
    "amount": 100
  },
  "occurredAt": "2026-02-26T12:00:00.0000000+00:00"
}
```

## Outbox + Idempotency Notes

- Orders service uses the outbox pattern:
  - `Order` and `OutboxMessage` are written in one DB transaction.
  - Background worker publishes only `Pending` outbox rows.
  - After successful publish, outbox row is marked `Published`.
- Notifications service is idempotent:
  - Checks `processed_events` by `eventId`.
  - If already processed, message is ACKed and skipped.
  - Unique index on `processed_events(event_id)` protects against duplicate processing races.
