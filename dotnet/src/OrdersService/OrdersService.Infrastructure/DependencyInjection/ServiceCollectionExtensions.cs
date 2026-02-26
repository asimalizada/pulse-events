using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrdersService.Infrastructure.Messaging;
using OrdersService.Infrastructure.Outbox;
using OrdersService.Infrastructure.Persistence;

namespace OrdersService.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrdersInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? configuration.GetConnectionString("OrdersDatabase")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'Default', 'OrdersDatabase', or 'DefaultConnection' is required.");

        services.AddDbContext<OrdersDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
        services.AddHostedService<OutboxPublisherHostedService>();

        services.AddHealthChecks()
            .AddDbContextCheck<OrdersDbContext>("postgres")
            .AddCheck<RabbitMqHealthCheck>("rabbitmq");

        return services;
    }
}
