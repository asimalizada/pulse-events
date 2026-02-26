import { Module } from '@nestjs/common';
import { PrismaService } from './prisma/prisma.service';
import { NotificationsController } from './notifications/notifications.controller';
import { NotificationsService } from './notifications/notifications.service';
import { OrdersConsumer } from './consumer/orders.consumer';

@Module({
  controllers: [NotificationsController],
  providers: [PrismaService, NotificationsService, OrdersConsumer],
})
export class AppModule {}
