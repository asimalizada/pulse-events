# EF Core Migrations Guide

This repository uses separate databases per service to preserve microservice isolation:

- OrdersService -> `orders_db`
- NotificationsService -> `notifications_db`

Connection strings are defined in:

- `src/OrdersService/OrdersService.Api/appsettings.json`
- `src/NotificationsService/NotificationsService.Api/appsettings.json`

## Automatic migrations (Development)

Both APIs apply migrations automatically at startup in Development only via `db.Database.Migrate()`.

- Orders: `src/OrdersService/OrdersService.Api/Program.cs`
- Notifications: `src/NotificationsService/NotificationsService.Api/Program.cs`

Run either API in Development and pending migrations will be applied.

## Manual migration apply (any environment)

From the `dotnet/` folder:

```powershell
# OrdersService

dotnet ef database update \
  --project src/OrdersService/OrdersService.Infrastructure/OrdersService.Infrastructure.csproj \
  --startup-project src/OrdersService/OrdersService.Api/OrdersService.Api.csproj \
  --context OrdersService.Infrastructure.Persistence.OrdersDbContext

# NotificationsService

dotnet ef database update \
  --project src/NotificationsService/NotificationsService.Infrastructure/NotificationsService.Infrastructure.csproj \
  --startup-project src/NotificationsService/NotificationsService.Api/NotificationsService.Api.csproj \
  --context NotificationsService.Infrastructure.Persistence.NotificationsDbContext
```

## Create databases (if they do not exist)

Example PostgreSQL commands:

```sql
CREATE DATABASE orders_db;
CREATE DATABASE notifications_db;
```

Ensure the DB user in connection strings has permissions for both databases.
