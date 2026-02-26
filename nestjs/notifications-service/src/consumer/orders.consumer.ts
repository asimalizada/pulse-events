import { Injectable, Logger, OnModuleDestroy, OnModuleInit } from '@nestjs/common';
import { Channel, ChannelModel, ConsumeMessage, Options, connect } from 'amqplib';
import { MESSAGING } from '../common/messaging.constants';
import { buildRabbitMqUrl } from '../common/rabbitmq';
import { OrderCreatedEvent } from '../common/contracts/order-created.event';
import { OrderCreatedProcessor } from './order-created.processor';

@Injectable()
export class OrdersConsumer implements OnModuleInit, OnModuleDestroy {
  private readonly logger = new Logger(OrdersConsumer.name);
  private readonly rabbitmqUrl = buildRabbitMqUrl();

  private connection: ChannelModel | null = null;
  private channel: Channel | null = null;

  constructor(private readonly processor: OrderCreatedProcessor) {}

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

  async canConnect(): Promise<boolean> {
    let connection: ChannelModel | null = null;
    let channel: Channel | null = null;

    try {
      connection = await connect(this.rabbitmqUrl);
      channel = await connection.createChannel();
      return true;
    } catch {
      return false;
    } finally {
      if (channel) {
        await channel.close();
      }
      if (connection) {
        await connection.close();
      }
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
    await this.channel.assertExchange(MESSAGING.exchange, 'topic', { durable: true });
    await this.channel.assertQueue(MESSAGING.queue, { durable: true });
    await this.channel.bindQueue(MESSAGING.queue, MESSAGING.exchange, MESSAGING.routingKey);
    await this.channel.prefetch(20);

    await this.channel.consume(MESSAGING.queue, (msg) => {
      void this.handleMessage(msg);
    });
  }

  private async handleMessage(msg: ConsumeMessage | null): Promise<void> {
    if (!msg || !this.channel) {
      return;
    }

    let event: OrderCreatedEvent;
    try {
      event = JSON.parse(msg.content.toString()) as OrderCreatedEvent;
    } catch {
      this.logger.warn('Skipping invalid JSON message.');
      this.channel.ack(msg);
      return;
    }

    if (event.type !== MESSAGING.eventTypeOrderCreated || !event.eventId || !event.data?.orderId || !event.data?.customerId) {
      this.logger.warn('Skipping invalid OrderCreated message.');
      this.channel.ack(msg);
      return;
    }

    try {
      const result = await this.processor.process(event);
      this.logger.log({
        message: 'Processed OrderCreated message.',
        status: result,
        eventId: event.eventId,
        orderId: event.data.orderId,
        correlationId: this.resolveCorrelationId(msg, event),
      });
      this.channel.ack(msg);
    } catch (error) {
      this.logger.error(
        `Failed processing event ${event.eventId}: ${(error as Error).message}`,
      );
      await this.retryOrReject(msg, event);
    }
  }

  private async retryOrReject(msg: ConsumeMessage, event: OrderCreatedEvent): Promise<void> {
    if (!this.channel) {
      return;
    }

    const retryCount = this.getRetryCount(msg.properties.headers);
    if (retryCount >= MESSAGING.maxRetries) {
      this.logger.error(
        `Max retries reached for event ${event.eventId}. Rejecting without requeue.`,
      );
      this.channel.nack(msg, false, false);
      return;
    }

    const headers = {
      ...(msg.properties.headers ?? {}),
      [MESSAGING.retryCountHeader]: retryCount + 1,
      [MESSAGING.correlationHeader]: this.resolveCorrelationId(msg, event),
    };

    const properties: Options.Publish = {
      ...msg.properties,
      headers,
      messageId: event.eventId,
      type: event.type,
      persistent: true,
      contentType: 'application/json',
    };

    this.channel.publish(MESSAGING.exchange, MESSAGING.routingKey, msg.content, properties);
    this.channel.ack(msg);
  }

  private getRetryCount(headers: Record<string, unknown> | undefined): number {
    if (!headers) {
      return 0;
    }

    const value = headers[MESSAGING.retryCountHeader];
    if (typeof value === 'number') {
      return value;
    }

    if (Buffer.isBuffer(value)) {
      const parsed = Number.parseInt(value.toString('utf8'), 10);
      return Number.isNaN(parsed) ? 0 : parsed;
    }

    return 0;
  }

  private resolveCorrelationId(msg: ConsumeMessage, event: OrderCreatedEvent): string {
    const headerValue = msg.properties.headers?.[MESSAGING.correlationHeader];
    if (typeof headerValue === 'string' && headerValue.length > 0) {
      return headerValue;
    }

    if (Buffer.isBuffer(headerValue)) {
      const parsed = headerValue.toString('utf8');
      if (parsed.length > 0) {
        return parsed;
      }
    }

    return event.correlationId;
  }
}
