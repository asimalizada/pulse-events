import { Injectable } from '@nestjs/common';
import { PrismaService } from '../prisma/prisma.service';

@Injectable()
export class NotificationsService {
  constructor(private readonly prisma: PrismaService) {}

  async getNotifications(orderId?: string) {
    return this.prisma.notification.findMany({
      where: orderId ? { orderId } : undefined,
      orderBy: { createdAt: 'desc' },
    });
  }
}
