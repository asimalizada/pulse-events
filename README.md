# pulse-events

A production-inspired event-driven microservices demo implemented in both .NET and NestJS.

This repository demonstrates asynchronous service-to-service communication using RabbitMQ, while applying real-world reliability patterns such as the Outbox Pattern and idempotent consumers.

The same architecture is implemented twice to showcase ecosystem flexibility:

- `/dotnet` → ASP.NET Core implementation
- `/nestjs` → NestJS implementation

---

## 🎯 Purpose

PulseEvents is not a CRUD demo.

It exists to demonstrate how distributed services communicate reliably using events and how to prevent common failure issues in asynchronous systems.

This project focuses on:

- Event-driven architecture
- Asynchronous communication
- Outbox pattern for reliable publishing
- Idempotent event consumption
- At-least-once delivery safety
- Dockerized local development

---

## 🏗 Architecture

```text
                ┌─────────────┐
                │   Client    │
                └──────┬──────┘
                       │
                       ▼
            ┌────────────────────┐
            │   Orders Service   │
            │ (writes order +    │
            │  outbox in same    │
            │  transaction)      │
            └─────────┬──────────┘
                      │
                      ▼
                ┌─────────────┐
                │  Database   │
                └─────────────┘
                      │
                      ▼
          ┌──────────────────────┐
          │ Outbox Publisher     │
          │ (background worker)  │
          └─────────┬────────────┘
                    │
                    ▼
              ┌────────────┐
              │ RabbitMQ   │
              └─────┬──────┘
                    │
                    ▼
          ┌──────────────────────┐
          │ Notifications Service│
          │ (idempotent consumer)│
          └─────────┬────────────┘
                    │
                    ▼
              ┌──────────────┐
              │ Notifications│
              │   Database   │
              └──────────────┘
```

---

## 📦 Tech Stack

### .NET Version
- .NET 8
- ASP.NET Core Web API
- EF Core
- PostgreSQL
- RabbitMQ
- BackgroundService workers
- Docker

### NestJS Version
- Node.js
- NestJS
- TypeORM / Prisma
- PostgreSQL
- RabbitMQ
- BullMQ (optional)
- Docker

---

## 🧩 Services

### Orders Service
- `POST /orders`
- Saves order in database
- Writes event to outbox table in same transaction
- Background publisher sends events to RabbitMQ

### Notifications Service
- Subscribes to `OrderCreated`
- Stores notification record
- Implements idempotency check using eventId tracking

---

## 🔐 Reliability Patterns Implemented

### ✔ Outbox Pattern
Ensures events are published only after successful DB commit.

### ✔ Idempotent Consumer
Prevents duplicate event processing during retries.

### ✔ At-least-once Delivery
Consumer safely handles redelivered messages.

---

## 🚀 Running Locally

### Run .NET version

```bash
cd dotnet
docker compose up --build
```

### Run NestJS version

```bash
cd nestjs
docker compose up --build
```

RabbitMQ and PostgreSQL are started automatically.

---

## 🧪 Example Event

```json
{
  "eventId": "uuid",
  "type": "OrderCreated",
  "data": {
    "orderId": "uuid",
    "customerId": "123",
    "amount": 100
  },
  "occurredAt": "2026-02-25T12:00:00Z"
}
```

---

## 📊 Future Improvements

- Dead-letter queue
- Retry policies with exponential backoff
- OpenTelemetry tracing
- Prometheus metrics
- Kubernetes deployment
- Saga pattern example

---

## 🧠 Design Decisions

- Publishing is decoupled from HTTP request lifecycle.
- Outbox guarantees transactional consistency.
- Consumers are idempotent for safety.
- Services are independently deployable.
- Same architecture across two ecosystems.

---

## 📄 License

This project is licensed under the MIT License.