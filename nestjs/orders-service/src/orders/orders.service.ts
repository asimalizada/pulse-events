import { BadRequestException, Injectable, Logger } from '@nestjs/common';
import { randomUUID } from 'node:crypto';
import { PrismaService } from '../prisma/prisma.service';
import { OrderCreatedEvent } from '../common/contracts/order-created.event';
import { MESSAGING, OUTBOX_STATUS } from '../common/messaging.constants';
import { CreateOrderDto } from './create-order.dto';

type CreateOrderResult = {
  orderId: string;
  eventId: string;
  correlationId: string;
};

@Injectable()
export class OrdersService {
  private readonly logger = new Logger(OrdersService.name);

  constructor(private readonly prisma: PrismaService) {}

  async createOrder(createOrderDto: CreateOrderDto, correlationId: string): Promise<CreateOrderResult> {
    this.validateCreateOrder(createOrderDto);

    const eventId = randomUUID();
    const occurredAt = new Date().toISOString();

    const created = await this.prisma.$transaction(async (tx) => {
      const order = await tx.order.create({
        data: {
          customerId: createOrderDto.customerId,
          amount: createOrderDto.amount,
        },
      });

      const eventPayload: OrderCreatedEvent = {
        eventId,
        type: MESSAGING.eventTypeOrderCreated,
        data: {
          orderId: order.id,
          customerId: order.customerId,
          amount: order.amount,
        },
        occurredAt,
        correlationId,
      };

      await tx.outboxEvent.create({
        data: {
          eventId,
          type: MESSAGING.eventTypeOrderCreated,
          payload: eventPayload,
          status: OUTBOX_STATUS.pending,
        },
      });

      return {
        orderId: order.id,
        eventId,
        correlationId,
      };
    });

    this.logger.log({
      message: 'Order and outbox persisted in one transaction.',
      orderId: created.orderId,
      eventId: created.eventId,
      correlationId: created.correlationId,
    });

    return created;
  }

  private validateCreateOrder(createOrderDto: CreateOrderDto): void {
    if (!createOrderDto || typeof createOrderDto.customerId !== 'string' || createOrderDto.customerId.trim().length === 0) {
      throw new BadRequestException('customerId is required');
    }

    if (typeof createOrderDto.amount !== 'number' || Number.isNaN(createOrderDto.amount)) {
      throw new BadRequestException('amount must be a number');
    }
  }
}
