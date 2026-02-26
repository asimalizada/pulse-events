import { Injectable } from '@nestjs/common';
import { connect } from 'amqplib';
import { buildRabbitMqUrl } from '../common/rabbitmq';
import { PrismaService } from '../prisma/prisma.service';

type DependencyStatus = {
  status: 'Healthy' | 'Unhealthy';
  error?: string;
};

@Injectable()
export class HealthService {
  constructor(private readonly prisma: PrismaService) {}

  async getHealth(): Promise<{ healthy: boolean; checks: Record<string, DependencyStatus> }> {
    const checks: Record<string, DependencyStatus> = {
      postgres: await this.checkPostgres(),
      rabbitmq: await this.checkRabbitMq(),
    };

    const healthy = Object.values(checks).every((check) => check.status === 'Healthy');
    return { healthy, checks };
  }

  private async checkPostgres(): Promise<DependencyStatus> {
    try {
      await this.prisma.$queryRaw`SELECT 1`;
      return { status: 'Healthy' };
    } catch (error) {
      return { status: 'Unhealthy', error: (error as Error).message };
    }
  }

  private async checkRabbitMq(): Promise<DependencyStatus> {
    let connection: any = null;
    let channel: any = null;

    try {
      connection = await connect(buildRabbitMqUrl());
      channel = await connection.createChannel();
      return { status: 'Healthy' };
    } catch (error) {
      return { status: 'Unhealthy', error: (error as Error).message };
    } finally {
      if (channel) {
        await channel.close();
      }
      if (connection) {
        await connection.close();
      }
    }
  }
}
