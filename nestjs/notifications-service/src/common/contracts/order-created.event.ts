export type OrderCreatedEvent = {
  eventId: string;
  type: 'OrderCreated';
  data: {
    orderId: string;
    customerId: string;
    amount: number;
  };
  occurredAt: string;
  correlationId: string;
};
