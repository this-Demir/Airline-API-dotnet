using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AirlineSystem.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace AirlineSystem.Infrastructure.Security;

/// <summary>
/// HMAC-SHA256 JWT implementation of <see cref="IJwtTokenGenerator"/>.
/// Reads signing parameters from <c>appsettings.json</c> (section
/// <c>JwtSettings</c>) via <see cref="IConfiguration"/> and produces
/// signed Bearer tokens for use in the <c>Authorization</c> header.
/// </summary>
/// <remarks>
/// <b>Required configuration keys:</b>
/// <list type="bullet">
///   <item><c>JwtSettings:Secret</c> — HMAC-SHA256 signing key (minimum 32 characters).</item>
///   <item><c>JwtSettings:Issuer</c> — Token issuer claim (<c>iss</c>).</item>
///   <item><c>JwtSettings:Audience</c> — Token audience claim (<c>aud</c>).</item>
///   <item><c>JwtSettings:ExpiryMinutes</c> — Token lifetime in minutes.</item>
/// </list>
/// <b>Claim strategy:</b> The role claim uses <see cref="ClaimTypes.Role"/>
/// (the long-form URI <c>http://schemas.microsoft.com/ws/2008/06/identity/claims/role</c>)
/// rather than the shorthand <c>"role"</c> string. ASP.NET Core's JWT Bearer
/// middleware maps this claim type automatically when the default
/// <c>TokenValidationParameters.RoleClaimType</c> is in effect, so
/// <c>[Authorize(Roles = "Admin")]</c> works without additional configuration
/// in the API layer.
/// </remarks>
public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initialises the token generator with the application configuration.
    /// </summary>
    /// <param name="configuration">
    /// The <see cref="IConfiguration"/> instance bound to <c>appsettings.json</c>
    /// (and environment-variable overrides). Must contain the <c>JwtSettings</c> section.
    /// </param>
    public JwtTokenGenerator(IConfiguration configuration) =>
        _configuration = configuration;

    /// <inheritdoc/>
    /// <remarks>
    /// <b>_requires:</b>
    /// <list type="bullet">
    ///   <item><c>JwtSettings:Secret</c> must be at least 32 characters (256 bits)
    ///   for HMAC-SHA256.</item>
    ///   <item><paramref name="userId"/> is the authenticated user's GUID.</item>
    ///   <item><paramref name="role"/> is the string representation of the user's
    ///   <c>UserRole</c> enum value (e.g., <c>"Admin"</c>, <c>"Customer"</c>).</item>
    /// </list>
    /// <b>_ensures:</b> Returns a compact, Base64Url-encoded JWT string valid for
    /// <c>JwtSettings:ExpiryMinutes</c> from the moment of creation.
    /// </remarks>
    public string GenerateToken(Guid userId, string email, string role)
    {
        var secret = _configuration["JwtSettings:Secret"]
            ?? throw new InvalidOperationException("JwtSettings:Secret is not configured.");
        var issuer = _configuration["JwtSettings:Issuer"]
            ?? throw new InvalidOperationException("JwtSettings:Issuer is not configured.");
        var audience = _configuration["JwtSettings:Audience"]
            ?? throw new InvalidOperationException("JwtSettings:Audience is not configured.");
        var expiryMinutes = int.Parse(
            _configuration["JwtSettings:ExpiryMinutes"] ?? "60");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
