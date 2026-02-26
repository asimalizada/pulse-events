# PulseEvents (NestJS)

Two NestJS microservices communicating via RabbitMQ using:
- Outbox Pattern (reliable publishing)
- Idempotent consumer (safe retries)

## Services
- orders-service (port 5001)
- notifications-service (port 5002)

## Run
```bash
docker compose up --build