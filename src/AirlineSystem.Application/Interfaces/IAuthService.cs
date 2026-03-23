using AirlineSystem.Application.DTOs.Auth;

namespace AirlineSystem.Application.Interfaces;

/// <summary>
/// Defines the Identity and Access Management (IAM) operations for the system (FR-01).
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Registers a new customer account and immediately issues a JWT.
    /// </summary>
    /// <param name="request">Registration payload containing the email and plain-text password.</param>
    /// <returns>
    /// An <see cref="AuthResponseDto"/> containing a signed JWT and the assigned role
    /// (<c>"Customer"</c>).
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when an account with the provided email already exists.
    /// </exception>
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request);

    /// <summary>
    /// Authenticates a user with email and password, and issues a JWT on success.
    /// </summary>
    /// <param name="request">Login payload containing the email and plain-text password.</param>
    /// <returns>
    /// An <see cref="AuthResponseDto"/> containing a signed JWT and the authenticated
    /// user's role.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when the email is not found or the password does not match the stored hash.
    /// The same exception message is used in both cases to prevent user enumeration.
    /// </exception>
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request);
}
