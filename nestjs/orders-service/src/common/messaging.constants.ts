export const MESSAGING = {
  exchange: 'pulse.events',
  queue: 'order.created',
  routingKey: 'order.created',
  eventTypeOrderCreated: 'OrderCreated',
  correlationHeader: 'x-correlation-id',
  retryCountHeader: 'x-retry-count',
} as const;

export const OUTBOX_STATUS = {
  pending: 'Pending',
  published: 'Published',
} as const;
