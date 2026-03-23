namespace AirlineSystem.Application.Interfaces;

/// <summary>
/// Abstracts JWT token generation to keep the Application layer free of
/// JWT library dependencies. Implemented in the Infrastructure layer using
/// the settings from <c>JwtSettings</c> in <c>appsettings.json</c>.
/// </summary>
public interface IJwtTokenGenerator
{
    /// <summary>
    /// Generates a signed JWT Bearer token for an authenticated user.
    /// </summary>
    /// <remarks>
    /// The token must embed the following claims at minimum:
    /// <list type="bullet">
    ///   <item><c>sub</c> — the user's GUID.</item>
    ///   <item><c>email</c> — the user's email address.</item>
    ///   <item><c>role</c> — the user's role string, used by ASP.NET Core
    ///   <c>[Authorize(Roles = "...")]</c> policy enforcement.</item>
    /// </list>
    /// Token lifetime is configured via <c>JwtSettings:ExpiryMinutes</c>.
    /// </remarks>
    /// <param name="userId">The GUID of the authenticated user (maps to the <c>sub</c> claim).</param>
    /// <param name="email">The user's email address.</param>
    /// <param name="role">The user's role string (e.g., <c>"Admin"</c>, <c>"Customer"</c>).</param>
    /// <returns>A signed, Base64-encoded JWT string ready for use in an Authorization header.</returns>
    string GenerateToken(Guid userId, string email, string role);
}
