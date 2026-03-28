# Build context: solution root (docker-compose sets context: .)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files first for layer-cached restore
COPY ["src/AirlineSystem.API/AirlineSystem.API.csproj", "src/AirlineSystem.API/"]
COPY ["src/AirlineSystem.Application/AirlineSystem.Application.csproj", "src/AirlineSystem.Application/"]
COPY ["src/AirlineSystem.Infrastructure/AirlineSystem.Infrastructure.csproj", "src/AirlineSystem.Infrastructure/"]
COPY ["src/AirlineSystem.Domain/AirlineSystem.Domain.csproj", "src/AirlineSystem.Domain/"]
RUN dotnet restore "src/AirlineSystem.API/AirlineSystem.API.csproj"

COPY . .
WORKDIR "/src/src/AirlineSystem.API"
RUN dotnet publish "AirlineSystem.API.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Run as non-root — ports below 1024 require root; use 8080 instead.
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser

COPY --from=build /app/publish .
RUN chown -R appuser:appgroup /app

USER appuser

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "AirlineSystem.API.dll"]
