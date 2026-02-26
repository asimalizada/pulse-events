import { Injectable, Logger, OnModuleDestroy, OnModuleInit } from '@nestjs/common';
import { Channel, ChannelModel, connect } from 'amqplib';
import { OrderCreatedEvent } from '../common/contracts/order-created.event';
import { MESSAGING, OUTBOX_STATUS } from '../common/messaging.constants';
import { buildRabbitMqUrl } from '../common/rabbitmq';
import { PrismaService } from '../prisma/prisma.service';

@Injectable()
export class OutboxPublisher implements OnModuleInit, OnModuleDestroy {
  private readonly logger = new Logger(OutboxPublisher.name);
  private readonly pollIntervalMs = 2000;
  private readonly publishAttempts = 3;
  private readonly rabbitmqUrl = buildRabbitMqUrl();

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

  async canConnect(): Promise<boolean> {
    const hasChannel = await this.ensureChannel();
    return hasChannel && this.channel !== null;
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
      await channel.assertExchange(MESSAGING.exchange, 'topic', { durable: true });
      await channel.assertQueue(MESSAGING.queue, { durable: true });
      await channel.bindQueue(MESSAGING.queue, MESSAGING.exchange, MESSAGING.routingKey);

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
        where: { status: OUTBOX_STATUS.pending },
        orderBy: { createdAt: 'asc' },
        take: 20,
      });

      for (const event of pendingEvents) {
        const published = await this.publishWithRetry(event.id, event.eventId, event.type, event.payload as OrderCreatedEvent);
        if (!published) {
          this.logger.error(`Outbox event remained pending after retries: ${event.id}`);
          continue;
        }

        await this.prisma.outboxEvent.updateMany({
          where: { id: event.id, status: OUTBOX_STATUS.pending },
          data: { status: OUTBOX_STATUS.published, publishedAt: new Date() },
        });
      }
    } catch (error) {
      this.logger.error(`Outbox publish cycle failed: ${(error as Error).message}`);
    } finally {
      this.isProcessing = false;
    }
  }

  private async publishWithRetry(
    outboxId: string,
    eventId: string,
    eventType: string,
    payload: OrderCreatedEvent,
  ): Promise<boolean> {
    let delayMs = 200;

    for (let attempt = 1; attempt <= this.publishAttempts; attempt++) {
      try {
        const hasChannel = await this.ensureChannel();
        if (!hasChannel || !this.channel) {
          throw new Error('RabbitMQ channel unavailable');
        }

        const message = Buffer.from(JSON.stringify(payload));
        this.channel.publish(MESSAGING.exchange, MESSAGING.routingKey, message, {
          persistent: true,
          contentType: 'application/json',
          messageId: eventId,
          type: eventType,
          headers: {
            [MESSAGING.correlationHeader]: payload.correlationId,
            [MESSAGING.retryCountHeader]: 0,
          },
        });

        return true;
      } catch (error) {
        if (attempt >= this.publishAttempts) {
          this.logger.error(
            `Publish failed permanently for outbox event ${outboxId} (eventId ${eventId}): ${(error as Error).message}`,
          );
          return false;
        }

        this.logger.warn(
          `Publish failed for outbox event ${outboxId} on attempt ${attempt}; retrying with backoff.`,
        );
        await new Promise((resolve) => setTimeout(resolve, delayMs));
        delayMs *= 2;
      }
    }

    return false;
  }
}
