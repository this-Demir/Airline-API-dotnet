using System.Net.Http.Json;
using AirlineSystem.Application.DTOs.Auth;
using Xunit;

namespace AirlineSystem.API.IntegrationTests;

/// <summary>
/// Base class for all integration test classes. Provides a shared
/// <see cref="HttpClient"/> and helper methods for obtaining JWT tokens
/// without duplicating setup logic across test classes.
/// </summary>
public abstract class IntegrationTestBase : IClassFixture<CustomWebApplicationFactory>
{
    /// <summary>The in-process test HTTP client for this test class.</summary>
    protected readonly HttpClient _client;

    /// <summary>The factory that owns the isolated in-memory database.</summary>
    protected readonly CustomWebApplicationFactory _factory;

    /// <summary>
    /// Initialises the base class with the factory provided by xUnit's
    /// <see cref="IClassFixture{TFixture}"/> mechanism.
    /// </summary>
    protected IntegrationTestBase(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    /// <summary>
    /// Registers a new <c>Customer</c> account with a unique email address
    /// and returns a signed JWT for that account.
    /// </summary>
    protected async Task<string> GetCustomerTokenAsync()
    {
        var email    = $"{Guid.NewGuid()}@test.com";
        const string password = "Customer123!";

        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password });
        var resp = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        var body = await resp.Content.ReadFromJsonAsync<AuthResponseDto>();
        return body!.Token;
    }

    /// <summary>
    /// Seeds an <c>Admin</c> user into the in-memory database (if not already present)
    /// and returns a signed JWT for that account.
    /// </summary>
    protected Task<string> GetAdminTokenAsync() => _factory.SeedAdminAsync(_client);

    /// <summary>
    /// Returns an <see cref="HttpRequestMessage"/> with the Authorization header
    /// pre-populated with the given Bearer token.
    /// </summary>
    protected static HttpRequestMessage Authorized(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
