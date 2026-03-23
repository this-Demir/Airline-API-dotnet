using AirlineSystem.Application.DTOs.Auth;
using Xunit;
using AirlineSystem.Application.Interfaces;
using AirlineSystem.Application.Services;
using AirlineSystem.Domain.Entities;
using AirlineSystem.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace AirlineSystem.Application.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUow = new();
    private readonly Mock<IUserRepository> _mockUserRepo = new();
    private readonly Mock<IPasswordHasher> _mockHasher = new();
    private readonly Mock<IJwtTokenGenerator> _mockJwt = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _mockUow.Setup(u => u.Users).Returns(_mockUserRepo.Object);
        _mockUow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _mockHasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed_password");
        _mockJwt.Setup(j => j.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns("test.jwt.token");

        _sut = new AuthService(_mockUow.Object, _mockHasher.Object, _mockJwt.Object);
    }

    // ── Register ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterAsync_NewEmail_ReturnsTokenAndCustomerRole()
    {
        // Arrange — EP: valid partition
        _mockUserRepo.Setup(r => r.GetByEmailAsync("new@example.com")).ReturnsAsync((User?)null);
        var request = new RegisterRequestDto { Email = "new@example.com", Password = "Password1!" };

        // Act
        var result = await _sut.RegisterAsync(request);

        // Assert
        result.Token.Should().Be("test.jwt.token");
        result.Role.Should().Be("Customer");
        _mockUserRepo.Verify(r => r.AddAsync(It.Is<User>(u => u.Email == "new@example.com")), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ThrowsInvalidOperationException()
    {
        // Arrange — State/uniqueness: email already in repo
        var existing = new User { Id = Guid.NewGuid(), Email = "taken@example.com" };
        _mockUserRepo.Setup(r => r.GetByEmailAsync("taken@example.com")).ReturnsAsync(existing);
        var request = new RegisterRequestDto { Email = "taken@example.com", Password = "Password1!" };

        // Act
        var act = () => _sut.RegisterAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already registered*");
    }

    [Fact]
    public async Task RegisterAsync_EmptyEmail_ThrowsArgumentException()
    {
        // Arrange — Boundary: empty email
        var request = new RegisterRequestDto { Email = "", Password = "Password1!" };

        // Act
        var act = () => _sut.RegisterAsync(request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RegisterAsync_EmptyPassword_ThrowsArgumentException()
    {
        // Arrange — Boundary: empty password
        var request = new RegisterRequestDto { Email = "user@example.com", Password = "" };

        // Act
        var act = () => _sut.RegisterAsync(request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── Login ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsToken()
    {
        // Arrange — EP: valid partition
        var user = new User { Id = Guid.NewGuid(), Email = "user@example.com", PasswordHash = "hashed_password" };
        _mockUserRepo.Setup(r => r.GetByEmailAsync("user@example.com")).ReturnsAsync(user);
        _mockHasher.Setup(h => h.Verify("correct_password", "hashed_password")).Returns(true);
        var request = new LoginRequestDto { Email = "user@example.com", Password = "correct_password" };

        // Act
        var result = await _sut.LoginAsync(request);

        // Assert
        result.Token.Should().Be("test.jwt.token");
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsUnauthorizedAccessException()
    {
        // Arrange — Negative: correct email, wrong password
        var user = new User { Id = Guid.NewGuid(), Email = "user@example.com", PasswordHash = "hashed_password" };
        _mockUserRepo.Setup(r => r.GetByEmailAsync("user@example.com")).ReturnsAsync(user);
        _mockHasher.Setup(h => h.Verify("wrong_password", "hashed_password")).Returns(false);
        var request = new LoginRequestDto { Email = "user@example.com", Password = "wrong_password" };

        // Act
        var act = () => _sut.LoginAsync(request);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Invalid email or password*");
    }
}
