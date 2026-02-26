import { BadRequestException, Controller, Get, Query } from '@nestjs/common';
import { NotificationsService } from './notifications.service';

@Controller('notifications')
export class NotificationsController {
  constructor(private readonly notificationsService: NotificationsService) {}

  @Get()
  async getNotifications(@Query('orderId') orderId?: string) {
    if (!orderId || orderId.trim().length === 0) {
      throw new BadRequestException('orderId query parameter is required');
    }

    return this.notificationsService.getNotifications(orderId);
  }
}
