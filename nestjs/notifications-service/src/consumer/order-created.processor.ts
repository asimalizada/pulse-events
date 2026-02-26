import { Injectable, Logger } from '@nestjs/common';
import { Prisma } from '@prisma/client';
import { OrderCreatedEvent } from '../common/contracts/order-created.event';
import { PrismaService } from '../prisma/prisma.service';

export type ProcessingResult = 'processed' | 'duplicate';

@Injectable()
export class OrderCreatedProcessor {
  private readonly logger = new Logger(OrderCreatedProcessor.name);

  constructor(private readonly prisma: PrismaService) {}

  async process(event: OrderCreatedEvent): Promise<ProcessingResult> {
    try {
      return await this.prisma.$transaction(async (tx) => {
        const existing = await tx.processedEvent.findUnique({
          where: { eventId: event.eventId },
        });

        if (existing) {
          this.logger.log({
            message: 'Duplicate event detected, skipping.',
            eventId: event.eventId,
            correlationId: event.correlationId,
          });
          return 'duplicate';
        }

        await tx.processedEvent.create({
          data: {
            eventId: event.eventId,
            processedAt: new Date(),
          },
        });

        await tx.notification.create({
          data: {
            orderId: event.data.orderId,
            message: `OrderCreated for customer ${event.data.customerId} amount ${event.data.amount}`,
          },
        });

        return 'processed';
      });
    } catch (error) {
      if (error instanceof Prisma.PrismaClientKnownRequestError && error.code === 'P2002') {
        return 'duplicate';
      }

      throw error;
    }
  }
}
