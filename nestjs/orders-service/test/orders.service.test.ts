import assert from 'node:assert/strict';
import { OrdersService } from '../src/orders/orders.service';

async function run(): Promise<void> {
  let outboxCreateInput: any;
  const prismaMock = {
    $transaction: async (callback: (tx: any) => Promise<unknown>) =>
      callback({
        order: {
          create: async ({ data }: any) => ({
            id: '11111111-1111-1111-1111-111111111111',
            customerId: data.customerId,
            amount: data.amount,
          }),
        },
        outboxEvent: {
          create: async ({ data }: any) => {
            outboxCreateInput = data;
            return data;
          },
        },
      }),
  };

  const service = new OrdersService(prismaMock as any);
  const correlationId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
  const result = await service.createOrder({ customerId: '123', amount: 100 }, correlationId);

  assert.equal(result.orderId, '11111111-1111-1111-1111-111111111111');
  assert.equal(outboxCreateInput.status, 'Pending');
  assert.equal(outboxCreateInput.type, 'OrderCreated');
  assert.equal(outboxCreateInput.payload.type, 'OrderCreated');
  assert.equal(outboxCreateInput.payload.correlationId, correlationId);
  assert.equal(outboxCreateInput.payload.data.customerId, '123');
  assert.equal(outboxCreateInput.payload.data.amount, 100);

  process.stdout.write('orders.service.test passed\n');
}

void run().catch((error) => {
  process.stderr.write(`orders.service.test failed: ${(error as Error).message}\n`);
  process.exit(1);
});
