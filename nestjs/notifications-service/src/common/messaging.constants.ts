export const MESSAGING = {
  exchange: 'pulse.events',
  queue: 'order.created',
  routingKey: 'order.created',
  eventTypeOrderCreated: 'OrderCreated',
  correlationHeader: 'x-correlation-id',
  retryCountHeader: 'x-retry-count',
  maxRetries: 5,
} as const;
