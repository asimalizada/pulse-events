FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/OrdersService/OrdersService.Api/OrdersService.Api.csproj", "src/OrdersService/OrdersService.Api/"]
COPY ["src/OrdersService/OrdersService.Infrastructure/OrdersService.Infrastructure.csproj", "src/OrdersService/OrdersService.Infrastructure/"]
COPY ["src/OrdersService/OrdersService.Domain/OrdersService.Domain.csproj", "src/OrdersService/OrdersService.Domain/"]
RUN dotnet restore "src/OrdersService/OrdersService.Api/OrdersService.Api.csproj"

COPY . .
RUN dotnet publish "src/OrdersService/OrdersService.Api/OrdersService.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "OrdersService.Api.dll"]
