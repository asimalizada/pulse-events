CREATE TABLE "notifications" (
  "id" TEXT NOT NULL,
  "order_id" TEXT NOT NULL,
  "message" TEXT NOT NULL,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  CONSTRAINT "notifications_pkey" PRIMARY KEY ("id")
);

CREATE INDEX "idx_notifications_order_id"
  ON "notifications" ("order_id");

CREATE TABLE "processed_events" (
  "id" TEXT NOT NULL,
  "event_id" TEXT NOT NULL,
  "event_type" TEXT NOT NULL,
  "processed_at" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  CONSTRAINT "processed_events_pkey" PRIMARY KEY ("id"),
  CONSTRAINT "processed_events_event_id_key" UNIQUE ("event_id")
);
