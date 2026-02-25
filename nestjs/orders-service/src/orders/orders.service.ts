import { BadRequestException, Injectable } from '@nestjs/common';
import { PrismaService } from '../prisma/prisma.service';
import { CreateOrderDto } from './create-order.dto';

@Injectable()
export class OrdersService {
  constructor(private readonly prisma: PrismaService) {}

  async createOrder(createOrderDto: CreateOrderDto) {
    this.validateCreateOrder(createOrderDto);

    return this.prisma.$transaction(async (tx) => {
      const order = await tx.order.create({
        data: {
          customerName: createOrderDto.customerName,
          amount: createOrderDto.amount,
        },
      });

      await tx.outboxEvent.create({
        data: {
          aggregateType: 'ORDER',
          aggregateId: order.id,
          eventType: 'OrderCreated',
          payload: {
            orderId: order.id,
            customerName: order.customerName,
            amount: order.amount,
            createdAt: order.createdAt.toISOString(),
          },
          status: 'PENDING',
        },
      });

      return order;
    });
  }

  private validateCreateOrder(createOrderDto: CreateOrderDto): void {
    if (!createOrderDto || typeof createOrderDto.customerName !== 'string' || createOrderDto.customerName.trim().length === 0) {
      throw new BadRequestException('customerName is required');
    }

    if (typeof createOrderDto.amount !== 'number' || Number.isNaN(createOrderDto.amount)) {
      throw new BadRequestException('amount must be a number');
    }
  }
}
