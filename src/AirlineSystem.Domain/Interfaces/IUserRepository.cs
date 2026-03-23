using AirlineSystem.Domain.Entities;

namespace AirlineSystem.Domain.Interfaces;

/// <summary>
/// Extends <see cref="IGenericRepository{User}"/> with identity-specific lookup operations.
/// </summary>
public interface IUserRepository : IGenericRepository<User>
{
    /// <summary>
    /// Retrieves a user by their unique email address.
    /// </summary>
    /// <remarks>
    /// Used by the authentication pipeline to locate accounts during login and to enforce
    /// uniqueness during registration. The email comparison must be case-insensitive.
    /// </remarks>
    /// <param name="email">The email address to search for.</param>
    /// <returns>
    /// The matching <see cref="User"/> entity, or <c>null</c> if no account exists
    /// with the given email.
    /// </returns>
    Task<User?> GetByEmailAsync(string email);
}
