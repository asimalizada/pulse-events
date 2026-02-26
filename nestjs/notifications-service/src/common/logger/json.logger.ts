import { LoggerService } from '@nestjs/common';

type LogLevel = 'log' | 'error' | 'warn' | 'debug' | 'verbose';

export class JsonLogger implements LoggerService {
  constructor(private readonly serviceName: string) {}

  log(message: unknown, context?: string): void {
    this.write('log', message, context);
  }

  error(message: unknown, trace?: string, context?: string): void {
    this.write('error', message, context, { trace });
  }

  warn(message: unknown, context?: string): void {
    this.write('warn', message, context);
  }

  debug(message: unknown, context?: string): void {
    this.write('debug', message, context);
  }

  verbose(message: unknown, context?: string): void {
    this.write('verbose', message, context);
  }

  private write(level: LogLevel, message: unknown, context?: string, extra?: Record<string, unknown>): void {
    const payload = {
      timestamp: new Date().toISOString(),
      level,
      service: this.serviceName,
      context,
      message: typeof message === 'string' ? message : JSON.stringify(message),
      ...extra,
    };

    const line = JSON.stringify(payload);
    if (level === 'error') {
      process.stderr.write(`${line}\n`);
      return;
    }

    process.stdout.write(`${line}\n`);
  }
}
