import assert from 'node:assert/strict';
import { OrderCreatedEvent } from '../src/common/contracts/order-created.event';
import { OrderCreatedProcessor } from '../src/consumer/order-created.processor';

async function run(): Promise<void> {
  const processedEvents = new Set<string>();
  const notifications: Array<{ orderId: string; message: string }> = [];

  const prismaMock = {
    $transaction: async (callback: (tx: any) => Promise<unknown>) =>
      callback({
        processedEvent: {
          findUnique: async ({ where }: any) => (processedEvents.has(where.eventId) ? { eventId: where.eventId } : null),
          create: async ({ data }: any) => {
            processedEvents.add(data.eventId);
            return data;
          },
        },
        notification: {
          create: async ({ data }: any) => {
            notifications.push({ orderId: data.orderId, message: data.message });
            return data;
          },
        },
      }),
  };

  const processor = new OrderCreatedProcessor(prismaMock as any);
  const event: OrderCreatedEvent = {
    eventId: 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee',
    type: 'OrderCreated',
    data: {
      orderId: '11111111-1111-1111-1111-111111111111',
      customerId: '123',
      amount: 99.5,
    },
    occurredAt: new Date().toISOString(),
    correlationId: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
  };

  const first = await processor.process(event);
  const second = await processor.process(event);

  assert.equal(first, 'processed');
  assert.equal(second, 'duplicate');
  assert.equal(processedEvents.size, 1);
  assert.equal(notifications.length, 1);

  process.stdout.write('order-created.processor.test passed\n');
}

void run().catch((error) => {
  process.stderr.write(`order-created.processor.test failed: ${(error as Error).message}\n`);
  process.exit(1);
});
