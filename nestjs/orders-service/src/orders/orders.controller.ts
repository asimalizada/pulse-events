import { Body, Controller, Headers, Post } from '@nestjs/common';
import { randomUUID } from 'node:crypto';
import { MESSAGING } from '../common/messaging.constants';
import { CreateOrderDto } from './create-order.dto';
import { OrdersService } from './orders.service';

@Controller('orders')
export class OrdersController {
  constructor(private readonly ordersService: OrdersService) {}

  @Post()
  async createOrder(
    @Body() createOrderDto: CreateOrderDto,
    @Headers(MESSAGING.correlationHeader) correlationIdHeader?: string,
  ) {
    const correlationId = correlationIdHeader && correlationIdHeader.length > 0 ? correlationIdHeader : randomUUID();
    return this.ordersService.createOrder(createOrderDto, correlationId);
  }
}
