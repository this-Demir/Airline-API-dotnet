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
RUN dotnet publish "AirlineSystem.API.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80
ENTRYPOINT ["dotnet", "AirlineSystem.API.dll"]
