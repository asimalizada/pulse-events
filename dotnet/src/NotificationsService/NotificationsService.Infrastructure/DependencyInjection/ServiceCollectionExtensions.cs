using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotificationsService.Infrastructure.Consumers;
using NotificationsService.Infrastructure.Persistence;

namespace NotificationsService.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? configuration.GetConnectionString("NotificationsDatabase")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'Default', 'NotificationsDatabase', or 'DefaultConnection' is required.");

        services.AddDbContext<NotificationsDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IOrderCreatedEventProcessor, OrderCreatedEventProcessor>();
        services.AddHostedService<OrderCreatedConsumerHostedService>();

        services.AddHealthChecks()
            .AddDbContextCheck<NotificationsDbContext>("postgres")
            .AddCheck<RabbitMqHealthCheck>("rabbitmq");

        return services;
    }
}
