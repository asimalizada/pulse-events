using Microsoft.EntityFrameworkCore;
using OrdersService.Api.Services;
using OrdersService.Infrastructure.DependencyInjection;
using OrdersService.Infrastructure.Messaging;
using OrdersService.Infrastructure.Outbox;
using OrdersService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOrdersPersistence(builder.Configuration);
builder.Services.AddScoped<IOrdersApplicationService, OrdersApplicationService>();
builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
builder.Services.AddHostedService<OutboxPublisherHostedService>();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
    dbContext.Database.Migrate();

    app.MapOpenApi();
}

app.MapControllers();

app.Run();
