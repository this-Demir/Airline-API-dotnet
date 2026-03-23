using AirlineSystem.Application.Interfaces;
using BC = BCrypt.Net.BCrypt;

namespace AirlineSystem.Infrastructure.Security;

/// <summary>
/// BCrypt implementation of <see cref="IPasswordHasher"/>.
/// Uses the <c>BCrypt.Net-Next</c> library (work factor 12 by default)
/// to produce and verify salted password hashes.
/// </summary>
/// <remarks>
/// <b>Why BCrypt:</b> BCrypt is deliberately slow (work factor controls cost),
/// making offline brute-force attacks impractical. The hash output is
/// self-contained — it embeds the algorithm identifier, work factor, and salt —
/// so no additional columns are required in the <c>User</c> table.
/// <para>
/// <b>Timing safety:</b> <see cref="Verify"/> delegates to
/// <c>BCrypt.Verify</c> which performs a constant-time comparison, preventing
/// timing-based user enumeration attacks as required by the interface contract.
/// </para>
/// </remarks>
public class PasswordHasher : IPasswordHasher
{
    /// <inheritdoc/>
    /// <remarks>
    /// <b>_requires:</b> <paramref name="password"/> is not null or empty.
    /// <b>_ensures:</b> The returned string is a full BCrypt hash string
    /// (60 characters, <c>$2a$</c> prefix) that embeds salt and work factor.
    /// Store this string directly in <c>User.PasswordHash</c>.
    /// </remarks>
    public string Hash(string password) =>
        BC.HashPassword(password);

    /// <inheritdoc/>
    /// <remarks>
    /// <b>_requires:</b> <paramref name="hash"/> was produced by <see cref="Hash"/>.
    /// <b>_ensures:</b> Runs in constant time regardless of where the mismatch
    /// occurs, preventing timing attacks. Returns <c>false</c> — never throws —
    /// when the hash is malformed.
    /// </remarks>
    public bool Verify(string password, string hash) =>
        BC.Verify(password, hash);
}
