using System.Reflection;
using System.Security.Claims;
using System.Text;
using AirlineSystem.Application.Interfaces;
using AirlineSystem.Application.Services;
using AirlineSystem.Infrastructure.Data;
using AirlineSystem.Infrastructure.Repositories;
using AirlineSystem.Infrastructure.Security;
using AirlineSystem.API.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ── EF Core + MySQL ──────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
builder.Services.AddDbContext<AirlineDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// ── Infrastructure implementations ──────────────────────────────────────────
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

// ── Application services ─────────────────────────────────────────────────────
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAirportService, AirportService>();
builder.Services.AddScoped<IFlightService, FlightService>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<ICheckInService, CheckInService>();

// ── JWT Authentication ───────────────────────────────────────────────────────
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["JwtSettings:Issuer"],
            ValidAudience            = builder.Configuration["JwtSettings:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:Secret"]!)),
            // Must match ClaimTypes.Role used in JwtTokenGenerator so that
            // [Authorize(Roles = "Admin")] works without extra configuration.
            RoleClaimType            = ClaimTypes.Role
        };
    });

builder.Services.AddAuthorization();

// ── Controllers + Swagger ────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "Airline Ticketing API",
        Version     = "v1",
        Description = "REST API for flight inventory management, ticket purchasing, and passenger check-in."
    });

    // Include controller XML documentation in Swagger UI
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));

    // JWT Bearer security definition — shows the green padlock in Swagger UI
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Enter your JWT token (without the 'Bearer ' prefix)."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ── Build & Pipeline ─────────────────────────────────────────────────────────
var app = builder.Build();

// Apply pending EF Core migrations on startup so the containerised app works
// against a fresh MySQL volume without a manual `dotnet ef database update`.
// Guard against InMemory provider used by integration tests — MigrateAsync is
// relational-only and throws on non-relational providers.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AirlineDbContext>();
    if (db.Database.IsRelational())
        await db.Database.MigrateAsync();
    else
        await db.Database.EnsureCreatedAsync();
}

await SeedEssentials.SeedAdminAsync(app.Services, app.Configuration);

// Global exception handler must be the first middleware so it catches
// exceptions from all subsequent layers.
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Airline Ticketing API v1"));

// HTTPS termination is handled at the reverse proxy (gateway) level.
// Authentication must precede Authorization.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();

// Required by WebApplicationFactory<Program> in integration tests.
public partial class Program { }
