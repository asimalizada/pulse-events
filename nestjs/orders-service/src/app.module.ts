import { Module } from '@nestjs/common';
import { OrdersController } from './orders/orders.controller';
import { OrdersService } from './orders/orders.service';
import { PrismaService } from './prisma/prisma.service';
import { OutboxPublisher } from './outbox/outbox.publisher';
import { HealthController } from './health/health.controller';
import { HealthService } from './health/health.service';

@Module({
  controllers: [OrdersController, HealthController],
  providers: [PrismaService, OrdersService, OutboxPublisher, HealthService],
})
export class AppModule {}
