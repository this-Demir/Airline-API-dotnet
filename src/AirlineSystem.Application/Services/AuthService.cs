using AirlineSystem.Application.DTOs.Auth;
using AirlineSystem.Application.Interfaces;
using AirlineSystem.Domain.Entities;
using AirlineSystem.Domain.Enums;

namespace AirlineSystem.Application.Services;

/// <summary>
/// Implements Identity and Access Management (IAM) operations: customer registration
/// and credential-based authentication (FR-01).
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenGenerator _jwt;

    public AuthService(IUnitOfWork uow, IPasswordHasher hasher, IJwtTokenGenerator jwt)
    {
        _uow = uow;
        _hasher = hasher;
        _jwt = jwt;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <b>PRE-CONDITIONS:</b>
    /// <list type="bullet">
    ///   <item>No existing <c>User</c> record shares the provided email address.</item>
    /// </list>
    /// <b>POST-CONDITIONS:</b>
    /// <list type="bullet">
    ///   <item>A new <c>User</c> entity is persisted with <c>Role = UserRole.Customer</c>.</item>
    ///   <item>The plain-text password is never stored; only its BCrypt hash is persisted.</item>
    ///   <item>A signed JWT containing the new user's claims is returned immediately.</item>
    /// </list>
    /// <b>BUSINESS RULES:</b>
    /// <list type="bullet">
    ///   <item>Self-registration is restricted to the <c>Customer</c> role (FR-01.01).
    ///   Admin accounts must be provisioned out-of-band.</item>
    /// </list>
    /// </remarks>
    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            throw new ArgumentException("Email and password must not be empty.");

        var existing = await _uow.Users.GetByEmailAsync(request.Email);
        if (existing is not null)
            throw new InvalidOperationException("Email is already registered.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = _hasher.Hash(request.Password),
            Role = UserRole.Customer,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.Users.AddAsync(user);
        await _uow.SaveChangesAsync();

        var token = _jwt.GenerateToken(user.Id, user.Email, user.Role.ToString());
        return new AuthResponseDto { Token = token, Role = user.Role.ToString() };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <b>PRE-CONDITIONS:</b>
    /// <list type="bullet">
    ///   <item>A <c>User</c> record must exist with the provided email.</item>
    ///   <item>The provided password must match the stored BCrypt hash.</item>
    /// </list>
    /// <b>POST-CONDITIONS:</b>
    /// <list type="bullet">
    ///   <item>A signed JWT containing the user's id, email, and role claims is returned.</item>
    ///   <item>No state is mutated; this is a read-only operation.</item>
    /// </list>
    /// <b>BUSINESS RULES:</b>
    /// <list type="bullet">
    ///   <item>Both "email not found" and "wrong password" produce the same
    ///   <see cref="UnauthorizedAccessException"/> message to prevent user enumeration (FR-01.02).</item>
    /// </list>
    /// </remarks>
    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
    {
        var user = await _uow.Users.GetByEmailAsync(request.Email)
            ?? throw new UnauthorizedAccessException("Invalid email or password.");

        if (!_hasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        var token = _jwt.GenerateToken(user.Id, user.Email, user.Role.ToString());
        return new AuthResponseDto { Token = token, Role = user.Role.ToString() };
    }
}
