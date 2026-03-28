# Build context: solution root (docker-compose sets context: .)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/AirlineSystem.Gateway/AirlineSystem.Gateway.csproj", "src/AirlineSystem.Gateway/"]
RUN dotnet restore "src/AirlineSystem.Gateway/AirlineSystem.Gateway.csproj"

COPY . .
WORKDIR "/src/src/AirlineSystem.Gateway"
RUN dotnet publish "AirlineSystem.Gateway.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Run as non-root — ports below 1024 require root; use 8080 instead.
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser

COPY --from=build /app/publish .
RUN chown -R appuser:appgroup /app

USER appuser

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "AirlineSystem.Gateway.dll"]
