using System.Net;
using System.Net.Http.Json;
using AirlineSystem.Application.DTOs.Auth;
using FluentAssertions;
using Xunit;

namespace AirlineSystem.API.IntegrationTests.Endpoints;

/// <summary>
/// Integration smoke tests for <c>POST /api/v1/auth/register</c> and
/// <c>POST /api/v1/auth/login</c> (FR-01).
/// </summary>
public class AuthEndpointsTests : IntegrationTestBase
{
    public AuthEndpointsTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Register ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_ValidPayload_Returns200WithToken()
    {
        // Arrange
        var payload = new { email = $"{Guid.NewGuid()}@test.com", password = "Pass123!" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", payload);
        var body     = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.Token.Should().NotBeNullOrEmpty();
        body.Role.Should().Be("Customer");
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns400()
    {
        // Arrange — register once successfully
        var payload = new { email = $"{Guid.NewGuid()}@test.com", password = "Pass123!" };
        await _client.PostAsJsonAsync("/api/v1/auth/register", payload);

        // Act — register again with the same email
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", payload);

        // Assert — duplicate email mapped to 400 by ExceptionHandlingMiddleware
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_CorrectCredentials_Returns200WithToken()
    {
        // Arrange
        var email    = $"{Guid.NewGuid()}@test.com";
        var payload  = new { email, password = "Pass123!" };
        await _client.PostAsJsonAsync("/api/v1/auth/register", payload);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", payload);
        var body     = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        // Arrange — register a valid user
        var email = $"{Guid.NewGuid()}@test.com";
        await _client.PostAsJsonAsync("/api/v1/auth/register",
            new { email, password = "CorrectPass1!" });

        // Act — login with a wrong password
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "WrongPass999!" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
