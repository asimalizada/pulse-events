import { Module } from '@nestjs/common';
import { OrdersController } from './orders/orders.controller';
import { OrdersService } from './orders/orders.service';
import { PrismaService } from './prisma/prisma.service';
import { OutboxPublisher } from './outbox/outbox.publisher';

@Module({
  controllers: [OrdersController],
  providers: [PrismaService, OrdersService, OutboxPublisher],
})
export class AppModule {}
