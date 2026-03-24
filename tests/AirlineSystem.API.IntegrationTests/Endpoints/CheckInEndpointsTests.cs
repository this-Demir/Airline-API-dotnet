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
/// Integration smoke tests for <c>POST /api/v1/checkin</c> (FR-06).
/// Verifies the endpoint is public, that business rejections surface as
/// <c>Status = "Failed"</c> (never as error status codes), and that the
/// already-checked-in guard works end-to-end through the real pipeline.
/// </summary>
public class CheckInEndpointsTests : IntegrationTestBase
{
    public CheckInEndpointsTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task CheckIn_IsPublicEndpoint_NoAuthRequired_Returns200()
    {
        // Arrange — deliberately omit the Authorization header
        var payload = new
        {
            pnrCode       = "GHOST01",
            passengerName = "Ghost Passenger"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/checkin", payload);

        // Assert — MUST be 200 (public endpoint per FR-06.04), not 401
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CheckIn_NoMatchingPassenger_Returns200WithFailedStatus()
    {
        // Arrange
        var payload = new
        {
            pnrCode       = "UNKNOWN",
            passengerName = "Unknown Person"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/checkin", payload);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert — HTTP 200 with business-level failure in the body (FR-06.03)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        doc.RootElement.GetProperty("status").GetString().Should().Be("Failed");
    }

    [Fact]
    public async Task CheckIn_ValidPnrAndPassenger_NotCheckedIn_Returns200WithSuccess()
    {
        // Arrange — seed full graph: Airport → Flight → User → Booking (PNR=HAPPY01) → Passenger (not checked in)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AirlineDbContext>();

            var origin = new Airport
            {
                Id = Guid.NewGuid(), Code = "IST3", Name = "Istanbul", City = "Istanbul",
                CreatedAt = DateTime.UtcNow
            };
            var destination = new Airport
            {
                Id = Guid.NewGuid(), Code = "ADB3", Name = "Izmir", City = "Izmir",
                CreatedAt = DateTime.UtcNow
            };
            var flight = new Flight
            {
                Id = Guid.NewGuid(), FlightNumber = "HAPPY01FLT",
                DepartureDate = new DateTime(2099, 10, 1, 10, 0, 0, DateTimeKind.Utc),
                ArrivalDate   = new DateTime(2099, 10, 1, 11, 0, 0, DateTimeKind.Utc),
                DurationMinutes = 60, TotalCapacity = 100, AvailableCapacity = 99,
                OriginAirportId = origin.Id, DestinationAirportId = destination.Id,
                CreatedAt = DateTime.UtcNow
            };
            var user = new Domain.Entities.User
            {
                Id = Guid.NewGuid(), Email = "happy@checkin.test",
                PasswordHash = "x", Role = Domain.Enums.UserRole.Customer,
                CreatedAt = DateTime.UtcNow
            };
            var booking = new Booking
            {
                Id = Guid.NewGuid(), PnrCode = "HAPPY01",
                TransactionDate = DateTime.UtcNow, UserId = user.Id,
                CreatedAt = DateTime.UtcNow
            };
            var passenger = new Passenger
            {
                Id = Guid.NewGuid(), FullName = "Happy Flyer",
                BookingId = booking.Id, FlightId = flight.Id,
                IsCheckedIn = false, SeatNumber = 0,
                CreatedAt = DateTime.UtcNow
            };

            db.Airports.AddRange(origin, destination);
            db.Users.Add(user);
            db.Flights.Add(flight);
            db.Bookings.Add(booking);
            db.Passengers.Add(passenger);
            await db.SaveChangesAsync();
        }

        var payload = new { pnrCode = "HAPPY01", passengerName = "Happy Flyer" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/checkin", payload);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        doc.RootElement.GetProperty("status").GetString().Should().Be("Success");
        doc.RootElement.GetProperty("seatNumber").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("fullName").GetString().Should().Be("Happy Flyer");
    }

    [Fact]
    public async Task CheckIn_ValidPnrWrongPassengerName_Returns200WithFailed()
    {
        // Arrange — same seed as above but we send a mismatched passenger name
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AirlineDbContext>();

            var origin = new Airport
            {
                Id = Guid.NewGuid(), Code = "IST4", Name = "Istanbul", City = "Istanbul",
                CreatedAt = DateTime.UtcNow
            };
            var destination = new Airport
            {
                Id = Guid.NewGuid(), Code = "ADB4", Name = "Izmir", City = "Izmir",
                CreatedAt = DateTime.UtcNow
            };
            var flight = new Flight
            {
                Id = Guid.NewGuid(), FlightNumber = "WRONG01FLT",
                DepartureDate = new DateTime(2099, 10, 2, 10, 0, 0, DateTimeKind.Utc),
                ArrivalDate   = new DateTime(2099, 10, 2, 11, 0, 0, DateTimeKind.Utc),
                DurationMinutes = 60, TotalCapacity = 100, AvailableCapacity = 99,
                OriginAirportId = origin.Id, DestinationAirportId = destination.Id,
                CreatedAt = DateTime.UtcNow
            };
            var user = new Domain.Entities.User
            {
                Id = Guid.NewGuid(), Email = "wrong@checkin.test",
                PasswordHash = "x", Role = Domain.Enums.UserRole.Customer,
                CreatedAt = DateTime.UtcNow
            };
            var booking = new Booking
            {
                Id = Guid.NewGuid(), PnrCode = "WRONG01",
                TransactionDate = DateTime.UtcNow, UserId = user.Id,
                CreatedAt = DateTime.UtcNow
            };
            var passenger = new Passenger
            {
                Id = Guid.NewGuid(), FullName = "Correct Name",
                BookingId = booking.Id, FlightId = flight.Id,
                IsCheckedIn = false, SeatNumber = 0,
                CreatedAt = DateTime.UtcNow
            };

            db.Airports.AddRange(origin, destination);
            db.Users.Add(user);
            db.Flights.Add(flight);
            db.Bookings.Add(booking);
            db.Passengers.Add(passenger);
            await db.SaveChangesAsync();
        }

        var payload = new { pnrCode = "WRONG01", passengerName = "Wrong Name" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/checkin", payload);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert — HTTP 200 with business-level failure (no matching passenger name)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        doc.RootElement.GetProperty("status").GetString().Should().Be("Failed");
    }

    [Fact]
    public async Task CheckIn_AlreadyCheckedIn_Returns200WithFailedStatus()
    {
        // Arrange — seed the minimum graph directly into the in-memory DB:
        // Airport → Flight → Booking → Passenger (already checked in)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AirlineDbContext>();

            var origin = new Airport
            {
                Id = Guid.NewGuid(), Code = "IST2", Name = "Istanbul", City = "Istanbul",
                CreatedAt = DateTime.UtcNow
            };
            var destination = new Airport
            {
                Id = Guid.NewGuid(), Code = "ADB2", Name = "Izmir", City = "Izmir",
                CreatedAt = DateTime.UtcNow
            };
            var flight = new Flight
            {
                Id = Guid.NewGuid(), FlightNumber = "CHKIN01",
                DepartureDate = new DateTime(2099, 7, 1, 10, 0, 0, DateTimeKind.Utc),
                ArrivalDate   = new DateTime(2099, 7, 1, 11, 0, 0, DateTimeKind.Utc),
                DurationMinutes = 60, TotalCapacity = 100, AvailableCapacity = 99,
                OriginAirportId = origin.Id, DestinationAirportId = destination.Id,
                CreatedAt = DateTime.UtcNow
            };
            var user = new Domain.Entities.User
            {
                Id = Guid.NewGuid(), Email = "seed@checkin.test",
                PasswordHash = "x", Role = Domain.Enums.UserRole.Customer,
                CreatedAt = DateTime.UtcNow
            };
            var booking = new Booking
            {
                Id = Guid.NewGuid(), PnrCode = "SEED01",
                TransactionDate = DateTime.UtcNow, UserId = user.Id,
                CreatedAt = DateTime.UtcNow
            };
            var passenger = new Passenger
            {
                Id = Guid.NewGuid(), FullName = "Already Checked",
                BookingId = booking.Id, FlightId = flight.Id,
                IsCheckedIn = true, SeatNumber = 1,
                CreatedAt = DateTime.UtcNow
            };

            db.Airports.AddRange(origin, destination);
            db.Users.Add(user);
            db.Flights.Add(flight);
            db.Bookings.Add(booking);
            db.Passengers.Add(passenger);
            await db.SaveChangesAsync();
        }

        var payload = new
        {
            pnrCode       = "SEED01",
            passengerName = "Already Checked"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/checkin", payload);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        doc.RootElement.GetProperty("status").GetString().Should().Be("Failed");
        doc.RootElement.GetProperty("message").GetString()
            .Should().Contain("already");
    }
}
