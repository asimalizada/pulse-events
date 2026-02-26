import { Module } from '@nestjs/common';
import { PrismaService } from './prisma/prisma.service';
import { NotificationsController } from './notifications/notifications.controller';
import { NotificationsService } from './notifications/notifications.service';
import { OrdersConsumer } from './consumer/orders.consumer';
import { OrderCreatedProcessor } from './consumer/order-created.processor';
import { HealthController } from './health/health.controller';
import { HealthService } from './health/health.service';

@Module({
  controllers: [NotificationsController, HealthController],
  providers: [PrismaService, NotificationsService, OrdersConsumer, OrderCreatedProcessor, HealthService],
})
export class AppModule {}
