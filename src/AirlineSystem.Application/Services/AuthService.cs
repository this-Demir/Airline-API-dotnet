using AirlineSystem.Application.DTOs.Auth;
using AirlineSystem.Application.Interfaces;
using AirlineSystem.Domain.Entities;
using AirlineSystem.Domain.Enums;

namespace AirlineSystem.Application.Services;

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

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request)
    {
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
