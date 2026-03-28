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

await app.UseOcelot();

app.Run();
