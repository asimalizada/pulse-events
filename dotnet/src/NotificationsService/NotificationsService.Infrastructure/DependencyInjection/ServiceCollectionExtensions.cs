using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotificationsService.Infrastructure.Persistence;

namespace NotificationsService.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationsPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? configuration.GetConnectionString("NotificationsDatabase")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'Default', 'NotificationsDatabase', or 'DefaultConnection' is required.");

        services.AddDbContext<NotificationsDbContext>(options =>
            options.UseNpgsql(connectionString));

        return services;
    }
}
