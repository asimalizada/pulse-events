export function buildRabbitMqUrl(): string {
  if (process.env.RABBITMQ_URL) {
    return process.env.RABBITMQ_URL;
  }

  const host = process.env.RABBITMQ_HOST ?? 'localhost';
  const port = process.env.RABBITMQ_PORT ?? '5672';
  const username = process.env.RABBITMQ_USERNAME ?? 'guest';
  const password = process.env.RABBITMQ_PASSWORD ?? 'guest';

  return `amqp://${encodeURIComponent(username)}:${encodeURIComponent(password)}@${host}:${port}`;
}
