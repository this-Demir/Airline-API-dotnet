using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AirlineSystem.Domain.Entities;
using AirlineSystem.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
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

    // Seed a minimal Airport → Flight graph into the in-memory DB and return the flight.
    private async Task<Flight> SeedFlightAsync(
        string flightNumber, DateTime departure, DateTime arrival, int totalCapacity, int availableCapacity)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AirlineDbContext>();

        var origin = new Airport
        {
            Id = Guid.NewGuid(), Code = $"O{Guid.NewGuid().ToString("N")[..2].ToUpper()}",
            Name = "Origin", City = "Origin City", CreatedAt = DateTime.UtcNow
        };
        var destination = new Airport
        {
            Id = Guid.NewGuid(), Code = $"D{Guid.NewGuid().ToString("N")[..2].ToUpper()}",
            Name = "Destination", City = "Dest City", CreatedAt = DateTime.UtcNow
        };
        var flight = new Flight
        {
            Id = Guid.NewGuid(), FlightNumber = flightNumber,
            DepartureDate = departure, ArrivalDate = arrival,
            DurationMinutes = (int)(arrival - departure).TotalMinutes,
            TotalCapacity = totalCapacity, AvailableCapacity = availableCapacity,
            OriginAirportId = origin.Id, DestinationAirportId = destination.Id,
            CreatedAt = DateTime.UtcNow
        };

        db.Airports.AddRange(origin, destination);
        db.Flights.Add(flight);
        await db.SaveChangesAsync();
        return flight;
    }

    [Fact]
    public async Task Purchase_NoToken_Returns401()
    {
        // Act — no Authorization header
        var response = await _client.PostAsJsonAsync("/api/v1/tickets/purchase", new
        {
            flightNumber   = "TK001",
            flightDate     = "2025-06-01T00:00:00",
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
            flightDate     = "2025-06-01T00:00:00",
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
            flightDate     = "2025-06-01T00:00:00",
            passengerNames = Array.Empty<string>()   // triggers ArgumentException in service
        });

        // Act
        var response = await _client.SendAsync(request);

        // Assert — TicketService throws ArgumentException → middleware maps to 400
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Business outcomes — seeded flights ───────────────────────────────────

    [Fact]
    public async Task Purchase_CustomerToken_ValidFlight_ReturnsConfirmedWithPnr()
    {
        // Arrange — seed a future flight with capacity
        var dep = new DateTime(2099, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        var arr = new DateTime(2099, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var flight = await SeedFlightAsync("TKPURCH01", dep, arr, totalCapacity: 10, availableCapacity: 10);

        var token   = await GetCustomerTokenAsync();
        var request = Authorized(HttpMethod.Post, "/api/v1/tickets/purchase", token);
        request.Content = JsonContent.Create(new
        {
            flightNumber   = flight.FlightNumber,
            flightDate     = dep.ToString("yyyy-MM-ddTHH:mm:ss"),
            passengerNames = new[] { "Alice Smith", "Bob Jones" }
        });

        // Act
        var response = await _client.SendAsync(request);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        doc.RootElement.GetProperty("status").GetString().Should().Be("Confirmed");
        doc.RootElement.GetProperty("pnrCode").GetString()!.Length.Should().Be(6);
    }

    [Fact]
    public async Task Purchase_CustomerToken_SoldOutFlight_ReturnsSoldOut()
    {
        // Arrange — seed a future flight with no capacity
        var dep = new DateTime(2099, 9, 2, 10, 0, 0, DateTimeKind.Utc);
        var arr = new DateTime(2099, 9, 2, 12, 0, 0, DateTimeKind.Utc);
        var flight = await SeedFlightAsync("TKSOLD01", dep, arr, totalCapacity: 100, availableCapacity: 0);

        var token   = await GetCustomerTokenAsync();
        var request = Authorized(HttpMethod.Post, "/api/v1/tickets/purchase", token);
        request.Content = JsonContent.Create(new
        {
            flightNumber   = flight.FlightNumber,
            flightDate     = dep.ToString("yyyy-MM-ddTHH:mm:ss"),
            passengerNames = new[] { "Charlie Brown" }
        });

        // Act
        var response = await _client.SendAsync(request);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert — HTTP 200 with business-level SoldOut (FR-05.04)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        doc.RootElement.GetProperty("status").GetString().Should().Be("SoldOut");
    }

    [Fact]
    public async Task Purchase_CustomerToken_PastFlight_Returns400()
    {
        // Arrange — seed a past flight (departure already passed)
        var dep = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var arr = new DateTime(2020, 1, 1, 2, 0, 0, DateTimeKind.Utc);
        var flight = await SeedFlightAsync("TKPAST01", dep, arr, totalCapacity: 100, availableCapacity: 100);

        var token   = await GetCustomerTokenAsync();
        var request = Authorized(HttpMethod.Post, "/api/v1/tickets/purchase", token);
        request.Content = JsonContent.Create(new
        {
            flightNumber   = flight.FlightNumber,
            flightDate     = dep.ToString("yyyy-MM-ddTHH:mm:ss"),
            passengerNames = new[] { "Dana White" }
        });

        // Act
        var response = await _client.SendAsync(request);

        // Assert — InvalidOperationException (past departure) → 400
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
