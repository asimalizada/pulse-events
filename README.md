# Pulse Events

Pulse Events is a dual-implementation microservices project designed to demonstrate senior-level distributed systems fundamentals: reliable event publishing (Outbox), safe at-least-once consumption (idempotent consumer), and operational observability (health checks, structured logs, correlation IDs). The same architecture is implemented in both NestJS and .NET to show framework-agnostic design thinking.

## Architecture

```text
Client
  |
  | POST /orders
  v
Orders API
  |-- write orders
  |-- write outbox_messages (same transaction)
  v
Outbox Worker (poll + publish + retry/backoff)
  v
RabbitMQ exchange: pulse.events
  routing key: order.created
  queue: order.created
  v
Notifications Consumer
  |-- insert processed_events(event_id unique)
  |-- insert notifications
  v
GET /notifications?orderId=<guid>
```

## Why These Patterns Matter

- Outbox pattern:
  Orders and integration events are committed atomically, so events are never published for rolled-back orders.
- Idempotent consumer:
  `processed_events(event_id)` prevents duplicate side effects under retries/redelivery.
- At-least-once delivery:
  Consumer acknowledges only after successful processing, so retries are expected and handled safely.
- Correlation ID propagation:
  Correlation ID is generated/accepted at order ingress, included in event payload and message headers, and logged in producer/consumer paths for traceability.

## Run

Both implementations use the same host ports for shared infrastructure. Run one stack at a time.

### NestJS

```bash
cd nestjs
docker compose up --build
```

- Orders API: `http://localhost:6101`
- Notifications API: `http://localhost:6102`
- RabbitMQ AMQP: `localhost:5673`
- RabbitMQ UI: `http://localhost:15673`

### .NET

```bash
cd dotnet
docker compose up --build
```

- Orders API: `http://localhost:6201`
- Notifications API: `http://localhost:6202`
- RabbitMQ AMQP: `localhost:5673`
- RabbitMQ UI: `http://localhost:15673`

## cURL Quick Test

### NestJS (Linux/macOS)

```bash
ORDER_RESPONSE=$(curl -s -X POST http://localhost:6101/orders \
  -H "Content-Type: application/json" \
  -H "x-correlation-id: 00000000-0000-0000-0000-00000000abcd" \
  -d '{"customerId":"customer-123","amount":77.5}')
echo "$ORDER_RESPONSE"

ORDER_ID=$(echo "$ORDER_RESPONSE" | sed -E 's/.*"orderId":"([^"]+)".*/\1/')
sleep 4
curl -s "http://localhost:6102/notifications?orderId=${ORDER_ID}"
```

### NestJS (Windows PowerShell)

```powershell
$body = @{ customerId = 'customer-123'; amount = 77.5 } | ConvertTo-Json -Compress
$order = Invoke-RestMethod -Method Post -Uri 'http://localhost:6101/orders' `
  -Headers @{ 'x-correlation-id' = '00000000-0000-0000-0000-00000000abcd' } `
  -ContentType 'application/json' -Body $body
$order
Start-Sleep -Seconds 4
Invoke-RestMethod -Method Get -Uri "http://localhost:6102/notifications?orderId=$($order.orderId)"
```

### .NET (Linux/macOS)

```bash
ORDER_RESPONSE=$(curl -s -X POST http://localhost:6201/orders \
  -H "Content-Type: application/json" \
  -H "x-correlation-id: 00000000-0000-0000-0000-000000000123" \
  -d '{"customerId":"customer-123","amount":100}')
echo "$ORDER_RESPONSE"

ORDER_ID=$(echo "$ORDER_RESPONSE" | sed -E 's/.*"orderId":"([^"]+)".*/\1/')
sleep 4
curl -s "http://localhost:6202/notifications?orderId=${ORDER_ID}"
```

### .NET (Windows PowerShell)

```powershell
$body = @{ customerId = 'customer-123'; amount = 100 } | ConvertTo-Json -Compress
$order = Invoke-RestMethod -Method Post -Uri 'http://localhost:6201/orders' `
  -Headers @{ 'x-correlation-id' = '00000000-0000-0000-0000-000000000123' } `
  -ContentType 'application/json' -Body $body
$order
Start-Sleep -Seconds 4
Invoke-RestMethod -Method Get -Uri "http://localhost:6202/notifications?orderId=$($order.orderId)"
```

## Expected Behavior

1. `POST /orders` returns `orderId`, `eventId`, and `correlationId`.
2. Orders DB stores order and outbox row in one transaction.
3. Outbox worker publishes `OrderCreated` to RabbitMQ.
4. Notifications consumer processes once, records `processed_events`, and writes notification.
5. `GET /notifications?orderId=...` returns one notification even if message is redelivered.

## Failure Handling Demonstration

1. Start either stack.
2. Stop RabbitMQ:
   `docker compose stop rabbitmq`
3. Create an order with `POST /orders`.
   Result: HTTP succeeds, outbox row stays `Pending` (event not lost).
4. Start RabbitMQ:
   `docker compose start rabbitmq`
5. Watch logs:
   outbox worker retries and eventually publishes; consumer processes idempotently.
6. Redelivery safety:
   restart notifications service; duplicate events are skipped due to `processed_events(event_id)` uniqueness.

## What I Would Improve In Production

1. Add DLQ and delayed-retry exchanges instead of immediate requeue.
2. Add OpenTelemetry traces and metrics dashboards (publish lag, retry counts, consumer latency).
3. Add authn/authz and secrets management (Vault/KMS), remove plaintext credentials.
4. Add contract versioning and schema registry validation.
5. Add chaos/integration tests with Testcontainers for broker/db failure scenarios.

## Repository Layout

```text
/dotnet   -> ASP.NET Core + EF Core implementation
/nestjs   -> NestJS + Prisma implementation
/LICENSE  -> MIT
```

## License

MIT License. See [LICENSE](LICENSE).
