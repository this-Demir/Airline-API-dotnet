using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace AirlineSystem.API.IntegrationTests.Endpoints;

/// <summary>
/// Integration smoke tests for <c>/api/v1/flights</c> endpoints (FR-02, FR-03, FR-04).
/// Verifies auth guards, public access on search, and error propagation via middleware.
/// </summary>
public class FlightEndpointsTests : IntegrationTestBase
{
    public FlightEndpointsTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Public search ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_PublicEndpoint_Returns200WithEmptyPage()
    {
        // Act — no auth token, no flights seeded in the in-memory DB
        var response = await _client.GetAsync(
            "/api/v1/flights/search?OriginCode=IST&DestinationCode=ADB" +
            "&DepartureFrom=2025-06-01&DepartureTo=2025-06-30&NumberOfPeople=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("totalCount").GetInt32().Should().Be(0);
        doc.RootElement.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    // ── Auth guards — Admin-only endpoints ───────────────────────────────────

    [Fact]
    public async Task GetAll_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/flights");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_CustomerToken_Returns403()
    {
        // Arrange
        var token   = await GetCustomerTokenAsync();
        var request = Authorized(HttpMethod.Get, "/api/v1/flights", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert — customer has no Admin role → 403 Forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_NoToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/flights",
            new { flightNumber = "TK001", departureDate = "2025-06-01" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Business logic — via Admin token ─────────────────────────────────────

    [Fact]
    public async Task Create_AdminToken_UnknownAirportCode_Returns404()
    {
        // Arrange — get a real Admin JWT
        var token = await GetAdminTokenAsync();
        var request = Authorized(HttpMethod.Post, "/api/v1/flights", token);
        request.Content = JsonContent.Create(new
        {
            flightNumber        = "TK001",
            departureDate       = DateTime.UtcNow.AddDays(10),
            arrivalDate         = DateTime.UtcNow.AddDays(10).AddHours(2),
            durationMinutes     = 120,
            originAirportCode   = "UNKNOWN_ORIGIN",
            destinationAirportCode = "UNKNOWN_DEST",
            totalCapacity       = 180
        });

        // Act
        var response = await _client.SendAsync(request);

        // Assert — FlightService throws KeyNotFoundException → middleware maps to 404
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
