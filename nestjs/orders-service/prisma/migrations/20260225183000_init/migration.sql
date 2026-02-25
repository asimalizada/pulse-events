CREATE TABLE "orders" (
  "id" TEXT NOT NULL,
  "customer_name" TEXT NOT NULL,
  "amount" DOUBLE PRECISION NOT NULL,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  CONSTRAINT "orders_pkey" PRIMARY KEY ("id")
);

CREATE TABLE "outbox_events" (
  "id" TEXT NOT NULL,
  "aggregate_type" TEXT NOT NULL,
  "aggregate_id" TEXT NOT NULL,
  "event_type" TEXT NOT NULL,
  "payload" JSONB NOT NULL,
  "status" TEXT NOT NULL DEFAULT 'PENDING',
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  "published_at" TIMESTAMPTZ,
  CONSTRAINT "outbox_events_pkey" PRIMARY KEY ("id"),
  CONSTRAINT "outbox_events_status_check" CHECK ("status" IN ('PENDING', 'PUBLISHED'))
);

CREATE INDEX "idx_outbox_status_created_at"
  ON "outbox_events" ("status", "created_at");
