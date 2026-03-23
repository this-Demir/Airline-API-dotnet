using AirlineSystem.Domain.Entities;
using AirlineSystem.Domain.Interfaces;
using AirlineSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AirlineSystem.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IUserRepository"/>.
/// Extends <see cref="GenericRepository{User}"/> with identity-specific
/// lookups required by the authentication pipeline (FR-01).
/// </summary>
public class UserRepository : GenericRepository<User>, IUserRepository
{
    /// <summary>
    /// Initialises the repository with the shared database context.
    /// </summary>
    /// <param name="context">The EF Core context scoped to the current request.</param>
    public UserRepository(AirlineDbContext context) : base(context) { }

    /// <inheritdoc/>
    /// <remarks>
    /// <b>_requires:</b> <paramref name="email"/> is not null or empty.
    /// <b>_ensures:</b> The comparison is case-insensitive so that
    /// <c>user@example.com</c> and <c>User@Example.COM</c> resolve to the same
    /// account. Used by <c>AuthService</c> for both login validation and
    /// duplicate-email detection during registration.
    /// </remarks>
    public async Task<User?> GetByEmailAsync(string email) =>
        await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
}
