import 'reflect-metadata';
import { NestFactory } from '@nestjs/core';
import { AppModule } from './app.module';
import { JsonLogger } from './common/logger/json.logger';

async function bootstrap(): Promise<void> {
  const app = await NestFactory.create(AppModule);
  app.useLogger(new JsonLogger('orders-service'));
  await app.listen(process.env.PORT ? Number(process.env.PORT) : 5001);
}

void bootstrap();
