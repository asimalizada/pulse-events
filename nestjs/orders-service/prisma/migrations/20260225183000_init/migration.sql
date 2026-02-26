CREATE TABLE "orders" (
  "id" TEXT NOT NULL,
  "customer_id" TEXT NOT NULL,
  "amount" DOUBLE PRECISION NOT NULL,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  CONSTRAINT "orders_pkey" PRIMARY KEY ("id")
);

CREATE TABLE "outbox_events" (
  "id" TEXT NOT NULL,
  "event_id" TEXT NOT NULL,
  "type" TEXT NOT NULL,
  "payload" JSONB NOT NULL,
  "status" TEXT NOT NULL DEFAULT 'Pending',
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  "published_at" TIMESTAMPTZ,
  CONSTRAINT "outbox_events_pkey" PRIMARY KEY ("id"),
  CONSTRAINT "outbox_events_event_id_key" UNIQUE ("event_id"),
  CONSTRAINT "outbox_events_status_check" CHECK ("status" IN ('Pending', 'Published'))
);

CREATE INDEX "idx_outbox_status_created_at"
  ON "outbox_events" ("status", "created_at");
