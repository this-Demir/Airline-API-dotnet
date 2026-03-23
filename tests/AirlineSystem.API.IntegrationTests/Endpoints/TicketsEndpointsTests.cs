using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AirlineSystem.API.IntegrationTests.Endpoints;

/// <summary>
/// Integration smoke tests for <c>POST /api/v1/tickets/purchase</c> (FR-05).
/// Verifies authentication requirement, flight-not-found propagation,
/// and empty-passenger-list validation.
/// </summary>
public class TicketsEndpointsTests : IntegrationTestBase
{
    public TicketsEndpointsTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Purchase_NoToken_Returns401()
    {
        // Act — no Authorization header
        var response = await _client.PostAsJsonAsync("/api/v1/tickets/purchase", new
        {
            flightNumber   = "TK001",
            date           = "2025-06-01T00:00:00",
            passengerNames = new[] { "Alice" }
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Purchase_CustomerToken_FlightNotFound_Returns404()
    {
        // Arrange
        var token   = await GetCustomerTokenAsync();
        var request = Authorized(HttpMethod.Post, "/api/v1/tickets/purchase", token);
        request.Content = JsonContent.Create(new
        {
            flightNumber   = "NONEXISTENT_FLIGHT",
            date           = "2025-06-01T00:00:00",
            passengerNames = new[] { "Alice" }
        });

        // Act
        var response = await _client.SendAsync(request);

        // Assert — TicketService throws KeyNotFoundException → middleware maps to 404
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Purchase_CustomerToken_EmptyPassengerList_Returns400()
    {
        // Arrange
        var token   = await GetCustomerTokenAsync();
        var request = Authorized(HttpMethod.Post, "/api/v1/tickets/purchase", token);
        request.Content = JsonContent.Create(new
        {
            flightNumber   = "TK001",
            date           = "2025-06-01T00:00:00",
            passengerNames = Array.Empty<string>()   // triggers ArgumentException in service
        });

        // Act
        var response = await _client.SendAsync(request);

        // Assert — TicketService throws ArgumentException → middleware maps to 400
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
