import { Injectable, Logger, OnModuleDestroy, OnModuleInit } from '@nestjs/common';
import { Channel, ChannelModel, connect } from 'amqplib';
import { PrismaService } from '../prisma/prisma.service';

@Injectable()
export class OutboxPublisher implements OnModuleInit, OnModuleDestroy {
  private readonly logger = new Logger(OutboxPublisher.name);
  private readonly pollIntervalMs = 2000;
  private readonly queueName = process.env.OUTBOX_QUEUE || 'orders.events';
  private readonly rabbitmqUrl = process.env.RABBITMQ_URL || 'amqp://rabbitmq:5672';

  private connection: ChannelModel | null = null;
  private channel: Channel | null = null;
  private pollTimer: NodeJS.Timeout | null = null;
  private isProcessing = false;

  constructor(private readonly prisma: PrismaService) {}

  async onModuleInit(): Promise<void> {
    await this.ensureChannel();

    this.pollTimer = setInterval(() => {
      void this.publishPendingEvents();
    }, this.pollIntervalMs);

    void this.publishPendingEvents();
  }

  async onModuleDestroy(): Promise<void> {
    if (this.pollTimer) {
      clearInterval(this.pollTimer);
      this.pollTimer = null;
    }

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

  private async ensureChannel(): Promise<boolean> {
    if (this.connection && this.channel) {
      return true;
    }

    try {
      const connection = await connect(this.rabbitmqUrl);
      connection.on('close', () => {
        this.logger.warn('RabbitMQ connection closed. Will retry on next poll.');
        this.connection = null;
        this.channel = null;
      });
      connection.on('error', (error) => {
        this.logger.error(`RabbitMQ connection error: ${(error as Error).message}`);
      });

      const channel = await connection.createChannel();
      await channel.assertQueue(this.queueName, { durable: true });

      this.connection = connection;
      this.channel = channel;
      return true;
    } catch (error) {
      this.logger.error(`Failed to connect to RabbitMQ at ${this.rabbitmqUrl}: ${(error as Error).message}`);
      this.connection = null;
      this.channel = null;
      return false;
    }
  }

  private async publishPendingEvents(): Promise<void> {
    if (this.isProcessing) {
      return;
    }

    this.isProcessing = true;

    try {
      const hasChannel = await this.ensureChannel();
      if (!hasChannel || !this.channel) {
        return;
      }

      const pendingEvents = await this.prisma.outboxEvent.findMany({
        where: { status: 'PENDING' },
        orderBy: { createdAt: 'asc' },
        take: 100,
      });

      for (const event of pendingEvents) {
        try {
          const message = Buffer.from(
            JSON.stringify({
              id: event.id,
              aggregateType: event.aggregateType,
              aggregateId: event.aggregateId,
              eventType: event.eventType,
              payload: event.payload,
              createdAt: event.createdAt.toISOString(),
            }),
          );

          this.channel.sendToQueue(this.queueName, message, {
            persistent: true,
            contentType: 'application/json',
            messageId: event.id,
            type: event.eventType,
          });

          await this.prisma.outboxEvent.updateMany({
            where: { id: event.id, status: 'PENDING' },
            data: { status: 'PUBLISHED', publishedAt: new Date() },
          });
        } catch (error) {
          this.logger.error(`Failed to publish outbox event ${event.id}: ${(error as Error).message}`);
        }
      }
    } catch (error) {
      this.logger.error(`Outbox publish cycle failed: ${(error as Error).message}`);
    } finally {
      this.isProcessing = false;
    }
  }
}
