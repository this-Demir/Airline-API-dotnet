# Build context: solution root (docker-compose sets context: .)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/AirlineSystem.Gateway/AirlineSystem.Gateway.csproj", "src/AirlineSystem.Gateway/"]
RUN dotnet restore "src/AirlineSystem.Gateway/AirlineSystem.Gateway.csproj"

COPY . .
WORKDIR "/src/src/AirlineSystem.Gateway"
RUN dotnet publish "AirlineSystem.Gateway.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80
ENTRYPOINT ["dotnet", "AirlineSystem.Gateway.dll"]
