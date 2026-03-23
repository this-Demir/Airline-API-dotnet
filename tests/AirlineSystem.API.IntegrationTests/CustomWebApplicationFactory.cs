using System.Net.Http.Json;
using AirlineSystem.Application.DTOs.Auth;
using AirlineSystem.Application.Interfaces;
using AirlineSystem.Domain.Entities;
using AirlineSystem.Domain.Enums;
using AirlineSystem.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AirlineSystem.API.IntegrationTests;

/// <summary>
/// Custom <see cref="WebApplicationFactory{TEntryPoint}"/> that replaces the MySQL
/// <see cref="AirlineDbContext"/> with an isolated EF Core in-memory database, enabling
/// the full HTTP pipeline (routing, auth, middleware, DI) to run in CI without a live
/// MySQL connection.
/// </summary>
/// <remarks>
/// Each factory instance creates a database named <c>TestDb_{Guid}</c> so that test
/// classes using <c>IClassFixture&lt;CustomWebApplicationFactory&gt;</c> each receive
/// an independent data store — cross-class state leakage is impossible.
/// </remarks>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Injecting dummy JWT settings directly into RAM so tests don't rely on appsettings.json
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                {"JwtSettings:Secret",   "IntegrationTestSecretKeyThatIsAtLeast32CharactersLong!"},
                {"JwtSettings:Issuer",   "TestIssuer"},
                {"JwtSettings:Audience", "TestAudience"}
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove the real MySQL DbContextOptions descriptor registered in Program.cs.
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AirlineDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            // Register an isolated in-memory database. The name is captured once so
            // all scopes (requests) within this factory share the same data store.
            var dbName = $"TestDb_{Guid.NewGuid()}";
            services.AddDbContext<AirlineDbContext>(options =>
                options.UseInMemoryDatabase(dbName));
        });
    }

    /// <summary>
    /// Seeds an <c>Admin</c> user directly into the in-memory database
    /// (bypassing <c>AuthService.RegisterAsync</c>, which only creates <c>Customer</c>
    /// accounts), then calls the login endpoint and returns the signed JWT.
    /// </summary>
    /// <param name="client">The <see cref="HttpClient"/> created by this factory.</param>
    /// <returns>A signed JWT Bearer token for the seeded admin account.</returns>
    public async Task<string> SeedAdminAsync(HttpClient client)
    {
        const string email    = "admin@integration.test";
        const string password = "Admin123!";

        using var scope  = Services.CreateScope();
        var db     = scope.ServiceProvider.GetRequiredService<AirlineDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        // Only seed once per factory lifetime; if the admin already exists, skip.
        if (!db.Users.Any(u => u.Email == email))
        {
            db.Users.Add(new User
            {
                Id           = Guid.NewGuid(),
                Email        = email,
                PasswordHash = hasher.Hash(password),
                Role         = UserRole.Admin,
                CreatedAt    = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var loginResp = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        var body      = await loginResp.Content.ReadFromJsonAsync<AuthResponseDto>();
        return body!.Token;
    }
}
