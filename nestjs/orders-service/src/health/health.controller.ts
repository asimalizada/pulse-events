import { Controller, Get, ServiceUnavailableException } from '@nestjs/common';
import { HealthService } from './health.service';

@Controller('health')
export class HealthController {
  constructor(private readonly healthService: HealthService) {}

  @Get()
  async getHealth() {
    const health = await this.healthService.getHealth();
    if (!health.healthy) {
      throw new ServiceUnavailableException({
        status: 'Unhealthy',
        checks: health.checks,
      });
    }

    return {
      status: 'Healthy',
      checks: health.checks,
    };
  }
}
