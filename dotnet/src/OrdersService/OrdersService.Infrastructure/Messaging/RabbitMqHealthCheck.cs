using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OrdersService.Infrastructure.Messaging;

public sealed class RabbitMqHealthCheck(IRabbitMqPublisher publisher) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var isReachable = await publisher.CanConnectAsync(cancellationToken);
        if (isReachable)
        {
            return HealthCheckResult.Healthy("RabbitMQ is reachable.");
        }

        return HealthCheckResult.Unhealthy("RabbitMQ is unreachable.");
    }
}
