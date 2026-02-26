FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/NotificationsService/NotificationsService.Api/NotificationsService.Api.csproj", "src/NotificationsService/NotificationsService.Api/"]
COPY ["src/NotificationsService/NotificationsService.Infrastructure/NotificationsService.Infrastructure.csproj", "src/NotificationsService/NotificationsService.Infrastructure/"]
COPY ["src/NotificationsService/NotificationsService.Domain/NotificationsService.Domain.csproj", "src/NotificationsService/NotificationsService.Domain/"]
RUN dotnet restore "src/NotificationsService/NotificationsService.Api/NotificationsService.Api.csproj"

COPY . .
RUN dotnet publish "src/NotificationsService/NotificationsService.Api/NotificationsService.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "NotificationsService.Api.dll"]
