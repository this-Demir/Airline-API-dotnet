namespace AirlineSystem.Application.Interfaces;

/// <summary>
/// Abstracts password hashing to keep the Application layer free of
/// cryptographic library dependencies. Implemented in the Infrastructure layer.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Produces a one-way cryptographic hash of the given plain-text password.
    /// </summary>
    /// <param name="password">The plain-text password to hash. Must not be null or empty.</param>
    /// <returns>
    /// A string containing the salted hash, suitable for persistent storage in
    /// <c>User.PasswordHash</c>. The hash is self-contained (includes algorithm,
    /// work factor, and salt) so no additional data must be stored separately.
    /// </returns>
    string Hash(string password);

    /// <summary>
    /// Verifies a plain-text password against a previously computed hash.
    /// </summary>
    /// <param name="password">The plain-text password submitted by the user.</param>
    /// <param name="hash">The stored hash from <c>User.PasswordHash</c>.</param>
    /// <returns>
    /// <c>true</c> if the password matches the hash; <c>false</c> otherwise.
    /// Must run in constant time to prevent timing-based enumeration attacks.
    /// </returns>
    bool Verify(string password, string hash);
}
