using Microsoft.EntityFrameworkCore;
using NotificationsService.Api.Services;
using NotificationsService.Infrastructure.Consumers;
using NotificationsService.Infrastructure.DependencyInjection;
using NotificationsService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNotificationsPersistence(builder.Configuration);
builder.Services.AddScoped<INotificationsQueryService, NotificationsQueryService>();
builder.Services.AddHostedService<OrderCreatedConsumerHostedService>();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
    dbContext.Database.Migrate();

    app.MapOpenApi();
}

app.MapControllers();

app.Run();
