using MMLib.SwaggerForOcelot.DependencyInjection;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Load ocelot.json into IConfiguration so both Ocelot and MMLib can read it.
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile(
    $"ocelot.{builder.Environment.EnvironmentName}.json",
    optional: true,
    reloadOnChange: true);

// Allow Azure App Service (or any host) to override the downstream API
// coordinates via configuration/env vars without modifying ocelot.json.
// Falls back to the Docker Compose service name so local runs are unaffected.
var apiHost   = builder.Configuration["ApiDownstream:Host"]   ?? "backend-api";
var apiPort   = builder.Configuration["ApiDownstream:Port"]   ?? "8080";
var apiScheme = builder.Configuration["ApiDownstream:Scheme"] ?? "http";

var routeCount = builder.Configuration.GetSection("Routes").GetChildren().Count();
for (var i = 0; i < routeCount; i++)
{
    builder.Configuration[$"Routes:{i}:DownstreamHostAndPorts:0:Host"] = apiHost;
    builder.Configuration[$"Routes:{i}:DownstreamHostAndPorts:0:Port"] = apiPort;
    builder.Configuration[$"Routes:{i}:DownstreamScheme"] = apiScheme;
}
builder.Configuration["SwaggerEndPoints:0:Config:0:Url"] =
    $"{apiScheme}://{apiHost}:{apiPort}/swagger/v1/swagger.json";

builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOcelot(builder.Configuration);
builder.Services.AddSwaggerForOcelot(builder.Configuration);

var app = builder.Build();

// Inject client IP as the rate-limit identifier so Ocelot can enforce
// per-IP quotas without requiring callers to send a ClientId header.
app.Use(async (context, next) =>
{
    var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    context.Request.Headers["ClientId"] = clientIp;
    await next();
});

// MMLib serves Swagger UI at /swagger and proxies downstream swagger.json.
// Must be registered BEFORE UseOcelot so its /swagger routes take priority.
app.UseSwaggerForOcelotUI();

// Health check must be mapped before UseOcelot — Ocelot is terminal middleware
// and swallows all requests that reach it, including /health.
app.MapHealthChecks("/health");

await app.UseOcelot();

app.Run();
