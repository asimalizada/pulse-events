import { Injectable, Logger, OnModuleDestroy, OnModuleInit } from '@nestjs/common';
import { Channel, ChannelModel, ConsumeMessage, connect } from 'amqplib';
import { Prisma } from '@prisma/client';
import { PrismaService } from '../prisma/prisma.service';

type OrderCreatedMessage = {
  id?: string;
  eventType?: string;
  aggregateId?: string;
  payload?: {
    orderId?: string;
    customerName?: string;
  };
};

@Injectable()
export class OrdersConsumer implements OnModuleInit, OnModuleDestroy {
  private readonly logger = new Logger(OrdersConsumer.name);
  private readonly rabbitmqUrl = process.env.RABBITMQ_URL || 'amqp://rabbitmq:5672';
  private readonly queueName = process.env.ORDERS_QUEUE || 'orders.events';

  private connection: ChannelModel | null = null;
  private channel: Channel | null = null;

  constructor(private readonly prisma: PrismaService) {}

  async onModuleInit(): Promise<void> {
    await this.startConsumer();
  }

  async onModuleDestroy(): Promise<void> {
    const channel = this.channel;
    const connection = this.connection;
    this.channel = null;
    this.connection = null;

    if (channel) {
      await channel.close();
    }

    if (connection) {
      await connection.close();
    }
  }

  private async startConsumer(): Promise<void> {
    this.connection = await connect(this.rabbitmqUrl);
    this.connection.on('close', () => {
      this.logger.warn('RabbitMQ connection closed.');
      this.connection = null;
      this.channel = null;
    });
    this.connection.on('error', (error) => {
      this.logger.error(`RabbitMQ connection error: ${(error as Error).message}`);
    });

    this.channel = await this.connection.createChannel();
    await this.channel.assertQueue(this.queueName, { durable: true });
    await this.channel.prefetch(20);

    await this.channel.consume(this.queueName, (msg) => {
      void this.handleMessage(msg);
    });
  }

  private async handleMessage(msg: ConsumeMessage | null): Promise<void> {
    if (!msg || !this.channel) {
      return;
    }

    let parsed: OrderCreatedMessage;

    try {
      parsed = JSON.parse(msg.content.toString()) as OrderCreatedMessage;
    } catch {
      this.logger.warn('Skipping invalid JSON message');
      this.channel.ack(msg);
      return;
    }

    const eventType = parsed.eventType || msg.properties.type;
    const eventId = parsed.id || msg.properties.messageId;
    const orderId = parsed.payload?.orderId || parsed.aggregateId;

    if (eventType !== 'OrderCreated' || !eventId || !orderId) {
      this.channel.ack(msg);
      return;
    }

    try {
      const existing = await this.prisma.processedEvent.findUnique({
        where: { eventId },
      });

      if (existing) {
        this.channel.ack(msg);
        return;
      }

      await this.prisma.$transaction(async (tx) => {
        await tx.processedEvent.create({
          data: {
            eventId,
            eventType: 'OrderCreated',
          },
        });

        await tx.notification.create({
          data: {
            orderId,
            message: `Notification created for order ${orderId}`,
          },
        });
      });

      this.channel.ack(msg);
    } catch (error) {
      if (
        error instanceof Prisma.PrismaClientKnownRequestError &&
        error.code === 'P2002'
      ) {
        this.channel.ack(msg);
        return;
      }

      this.logger.error(`Failed processing OrderCreated event ${eventId}: ${(error as Error).message}`);
      this.channel.nack(msg, false, true);
    }
  }
}
