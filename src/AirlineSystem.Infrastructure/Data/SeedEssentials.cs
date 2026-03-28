using AirlineSystem.Application.Interfaces;
using AirlineSystem.Domain.Entities;
using AirlineSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AirlineSystem.Infrastructure.Data;

/// <summary>
/// One-time startup seeder that creates the initial Admin account when the
/// database is empty. Credentials are read from configuration / environment
/// variables — never hardcoded.
/// </summary>
/// <remarks>
/// Reads <c>SeedAdmin:Email</c> and <c>SeedAdmin:Password</c> from
/// <see cref="IConfiguration"/> (map to env vars <c>SeedAdmin__Email</c> and
/// <c>SeedAdmin__Password</c> in Docker / docker-compose). If either value is
/// absent the method returns without touching the database, so the seeder is
/// completely opt-in.
/// </remarks>
public static class SeedEssentials
{
    /// <summary>
    /// Seeds a single Admin user if no Admin account exists yet.
    /// Safe to call on every startup — exits immediately when an Admin is
    /// already present (idempotent).
    /// </summary>
    public static async Task SeedAdminAsync(IServiceProvider services, IConfiguration configuration)
    {
        var email    = configuration["SeedAdmin:Email"];
        var password = configuration["SeedAdmin:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        using var scope  = services.CreateScope();
        var db     = scope.ServiceProvider.GetRequiredService<AirlineDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        if (await db.Users.AnyAsync(u => u.Role == UserRole.Admin))
            return;

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
}
