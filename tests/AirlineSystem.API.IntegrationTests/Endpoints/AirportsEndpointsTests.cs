using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace AirlineSystem.API.IntegrationTests.Endpoints;

/// <summary>
/// Integration smoke tests for <c>/api/v1/airports</c> CRUD endpoints (FR-02).
/// Verifies Admin-only access and the happy-path airport creation flow.
/// </summary>
public class AirportsEndpointsTests : IntegrationTestBase
{
    public AirportsEndpointsTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Auth guards ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/airports");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_CustomerToken_Returns403()
    {
        // Arrange
        var token   = await GetCustomerTokenAsync();
        var request = Authorized(HttpMethod.Get, "/api/v1/airports", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_NoToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/airports",
            new { code = "TST", name = "Test Airport", city = "Testville" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Happy path — Admin ───────────────────────────────────────────────────

    [Fact]
    public async Task Create_AdminToken_Returns201WithLocationAndBody()
    {
        // Arrange
        var token   = await GetAdminTokenAsync();
        var request = Authorized(HttpMethod.Post, "/api/v1/airports", token);
        request.Content = JsonContent.Create(new
        {
            code = "TST",
            name = "Test Airport",
            city = "Testville"
        });

        // Act
        var response = await _client.SendAsync(request);

        // Assert — 201 Created with Location header pointing to the new resource
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be("TST");
        doc.RootElement.GetProperty("city").GetString().Should().Be("Testville");
    }
}
