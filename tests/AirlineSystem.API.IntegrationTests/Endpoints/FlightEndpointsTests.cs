using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AirlineSystem.Domain.Entities;
using AirlineSystem.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AirlineSystem.API.IntegrationTests.Endpoints;

/// <summary>
/// Integration smoke tests for <c>/api/v1/flights</c> endpoints (FR-02, FR-03, FR-04).
/// Verifies auth guards, public access on search, and error propagation via middleware.
/// </summary>
public class FlightEndpointsTests : IntegrationTestBase
{
    public FlightEndpointsTests(CustomWebApplicationFactory factory) : base(factory) { }

    /// <summary>
    /// Seeds an Airport-pair and a Flight into the in-memory DB and returns the Flight.
    /// </summary>
    private async Task<Flight> SeedFlightAsync(
        string originCode, string destinationCode,
        string flightNumber, DateTime departure, int availableCapacity = 100)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AirlineDbContext>();

        var origin = new Airport
        {
            Id = Guid.NewGuid(), Code = originCode, Name = $"{originCode} Airport",
            City = originCode, CreatedAt = DateTime.UtcNow
        };
        var destination = new Airport
        {
            Id = Guid.NewGuid(), Code = destinationCode, Name = $"{destinationCode} Airport",
            City = destinationCode, CreatedAt = DateTime.UtcNow
        };
        var arrival = departure.AddHours(2);
        var flight = new Flight
        {
            Id = Guid.NewGuid(), FlightNumber = flightNumber,
            DepartureDate = departure, ArrivalDate = arrival,
            DurationMinutes = 120, TotalCapacity = 100, AvailableCapacity = availableCapacity,
            OriginAirportId = origin.Id, DestinationAirportId = destination.Id,
            CreatedAt = DateTime.UtcNow
        };

        db.Airports.AddRange(origin, destination);
        db.Flights.Add(flight);
        await db.SaveChangesAsync();
        return flight;
    }

    // ── Public search ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_PublicEndpoint_Returns200WithEmptyOutbound()
    {
        // Act — no auth token, no flights seeded in the in-memory DB
        var response = await _client.GetAsync(
            "/api/v1/flights/search?OriginCode=IST&DestinationCode=ADB" +
            "&DepartureFrom=2025-06-01&DepartureTo=2025-06-30&NumberOfPeople=1");

        // Assert — response shape is now { outbound: { items, totalCount, ... }, returnFlights: null }
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var outbound = doc.RootElement.GetProperty("outbound");
        outbound.GetProperty("totalCount").GetInt32().Should().Be(0);
        outbound.GetProperty("items").GetArrayLength().Should().Be(0);
        doc.RootElement.GetProperty("returnFlights").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Search_NoQueryParams_Returns200UsingDefaults()
    {
        // Arrange — seed a flight 10 days from now (inside the default 6-month window)
        await SeedFlightAsync("DF01", "DF02", "DFLT01",
            DateTime.UtcNow.AddDays(10));

        // Act — call with absolutely no query parameters
        var response = await _client.GetAsync("/api/v1/flights/search");

        // Assert — defaults kick in: today → today+6months, all origins/destinations, 1 passenger
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("outbound").GetProperty("totalCount").GetInt32()
            .Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Search_InvalidDateFormat_Returns400()
    {
        // Act — DepartureFrom value is not yyyy-MM-dd
        var response = await _client.GetAsync("/api/v1/flights/search?DepartureFrom=not-a-date");

        // Assert — ArgumentException thrown in service → ExceptionHandlingMiddleware → 400
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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

    // ── Public search — seeded flights ───────────────────────────────────────

    [Fact]
    public async Task Search_SeededFlight_EmptyCodes_ReturnsResults()
    {
        // Arrange — seed a future flight; search without code filters
        await SeedFlightAsync("SR01", "DS01", "SRCH01",
            new DateTime(2099, 11, 1, 10, 0, 0, DateTimeKind.Utc));

        // Act
        var response = await _client.GetAsync(
            "/api/v1/flights/search?DepartureFrom=2099-11-01&DepartureTo=2099-11-30&NumberOfPeople=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("outbound").GetProperty("totalCount").GetInt32()
            .Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Search_SeededFlight_OriginDestCodes_FiltersCorrectly()
    {
        // Arrange — seed a future flight with unique codes
        await SeedFlightAsync("SR02", "DS02", "SRCH02",
            new DateTime(2099, 11, 2, 10, 0, 0, DateTimeKind.Utc));

        // Act — filter by exact origin/destination codes
        var response = await _client.GetAsync(
            "/api/v1/flights/search?OriginCode=SR02&DestinationCode=DS02" +
            "&DepartureFrom=2099-11-01&DepartureTo=2099-11-30&NumberOfPeople=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("outbound").GetProperty("totalCount").GetInt32()
            .Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Search_SeededFlight_WrongCodes_ReturnsEmpty()
    {
        // Arrange — seed a future flight
        await SeedFlightAsync("SR03", "DS03", "SRCH03",
            new DateTime(2099, 11, 3, 10, 0, 0, DateTimeKind.Utc));

        // Act — filter by non-existent codes
        var response = await _client.GetAsync(
            "/api/v1/flights/search?OriginCode=XXX&DestinationCode=YYY" +
            "&DepartureFrom=2099-11-01&DepartureTo=2099-11-30&NumberOfPeople=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("outbound").GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Search_SeededFlightBothDirections_IsRoundTrip_ReturnsBothLegs()
    {
        // Arrange — seed outbound and return legs
        await SeedFlightAsync("SR04", "DS04", "SRCH04F",
            new DateTime(2099, 11, 4, 10, 0, 0, DateTimeKind.Utc));
        await SeedFlightAsync("DS04", "SR04", "SRCH04R",
            new DateTime(2099, 11, 11, 10, 0, 0, DateTimeKind.Utc));

        // Act
        var response = await _client.GetAsync(
            "/api/v1/flights/search?IsRoundTrip=true&OriginCode=SR04&DestinationCode=DS04" +
            "&DepartureFrom=2099-11-01&DepartureTo=2099-11-30&NumberOfPeople=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("outbound").GetProperty("totalCount").GetInt32()
            .Should().BeGreaterThanOrEqualTo(1);
        doc.RootElement.GetProperty("returnFlights").GetProperty("totalCount").GetInt32()
            .Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Search_NumberOfPeopleExceedsCapacity_FiltersOutFlight()
    {
        // Arrange — seed a flight with capacity 1
        await SeedFlightAsync("SR05", "DS05", "SRCH05",
            new DateTime(2099, 11, 5, 10, 0, 0, DateTimeKind.Utc), availableCapacity: 1);

        // Act — request 5 seats
        var response = await _client.GetAsync(
            "/api/v1/flights/search?OriginCode=SR05&DestinationCode=DS05" +
            "&DepartureFrom=2099-11-01&DepartureTo=2099-11-30&NumberOfPeople=5");

        // Assert — flight excluded because availableCapacity (1) < numberOfPeople (5)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("outbound").GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    // ── Admin CRUD ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_AdminToken_Returns200WithArray()
    {
        // Arrange
        var token   = await GetAdminTokenAsync();
        var request = Authorized(HttpMethod.Get, "/api/v1/flights", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetById_AdminToken_ExistingId_Returns200WithFlightNumber()
    {
        // Arrange — seed a flight, capture its Id
        var flight = await SeedFlightAsync("SR06", "DS06", "SRCH06",
            new DateTime(2099, 11, 6, 10, 0, 0, DateTimeKind.Utc));

        var token   = await GetAdminTokenAsync();
        var request = Authorized(HttpMethod.Get, $"/api/v1/flights/{flight.Id}", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("flightNumber").GetString().Should().Be("SRCH06");
    }

    [Fact]
    public async Task GetById_AdminToken_UnknownId_Returns404()
    {
        // Arrange
        var token   = await GetAdminTokenAsync();
        var request = Authorized(HttpMethod.Get, $"/api/v1/flights/{Guid.NewGuid()}", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_AdminToken_ValidData_Returns204()
    {
        // Arrange — seed airports and flight
        var flight = await SeedFlightAsync("SR07", "DS07", "SRCH07",
            new DateTime(2099, 11, 7, 10, 0, 0, DateTimeKind.Utc));

        var token  = await GetAdminTokenAsync();
        var put    = Authorized(HttpMethod.Put, $"/api/v1/flights/{flight.Id}", token);
        put.Content = JsonContent.Create(new
        {
            flightNumber           = "SRCH07UPD",
            departureDate          = new DateTime(2099, 11, 7, 11, 0, 0, DateTimeKind.Utc),
            arrivalDate            = new DateTime(2099, 11, 7, 13, 0, 0, DateTimeKind.Utc),
            durationMinutes        = 120,
            originAirportCode      = "SR07",
            destinationAirportCode = "DS07",
            totalCapacity          = 150
        });

        // Act
        var response = await _client.SendAsync(put);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Update_AdminToken_UnknownId_Returns404()
    {
        // Arrange — seed airports so the DTO codes resolve correctly
        await SeedFlightAsync("SR08", "DS08", "SRCH08DUMMY",
            new DateTime(2099, 11, 8, 10, 0, 0, DateTimeKind.Utc));

        var token  = await GetAdminTokenAsync();
        var put    = Authorized(HttpMethod.Put, $"/api/v1/flights/{Guid.NewGuid()}", token);
        put.Content = JsonContent.Create(new
        {
            flightNumber           = "NOEXIST",
            departureDate          = new DateTime(2099, 11, 8, 10, 0, 0, DateTimeKind.Utc),
            arrivalDate            = new DateTime(2099, 11, 8, 12, 0, 0, DateTimeKind.Utc),
            durationMinutes        = 120,
            originAirportCode      = "SR08",
            destinationAirportCode = "DS08",
            totalCapacity          = 100
        });

        // Act
        var response = await _client.SendAsync(put);

        // Assert — flight id not found → KeyNotFoundException → 404
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_AdminToken_ValidId_Returns204()
    {
        // Arrange — seed a flight
        var flight = await SeedFlightAsync("SR09", "DS09", "SRCH09",
            new DateTime(2099, 11, 9, 10, 0, 0, DateTimeKind.Utc));

        var token  = await GetAdminTokenAsync();
        var del    = Authorized(HttpMethod.Delete, $"/api/v1/flights/{flight.Id}", token);

        // Act
        var response = await _client.SendAsync(del);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── CSV Upload ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_NoToken_Returns401()
    {
        // Arrange — valid CSV content but no Authorization header
        var csv         = "FlightNumber,DepartureDate,ArrivalDate,DurationMinutes,OriginAirportCode,DestinationAirportCode,TotalCapacity\n" +
                          "TKUP99,2099-08-01 10:00,2099-08-01 12:00,120,AAA,BBB,100";
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
        var form = new MultipartFormDataContent();
        form.Add(fileContent, "file", "flights.csv");

        // Act
        var response = await _client.PostAsync("/api/v1/flights/upload", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Upload_AdminToken_NoFile_Returns400()
    {
        // Arrange — send a multipart request with no file part
        var token  = await GetAdminTokenAsync();
        var req    = Authorized(HttpMethod.Post, "/api/v1/flights/upload", token);
        req.Content = new MultipartFormDataContent();   // empty multipart — no "file" part

        // Act
        var response = await _client.SendAsync(req);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_AdminToken_ValidCsv_Returns200WithSuccessCount()
    {
        // Arrange — seed the airports referenced by the CSV
        await SeedFlightAsync("CSVO", "CSVD", "CSVDUMMY",
            new DateTime(2099, 7, 1, 10, 0, 0, DateTimeKind.Utc));  // seeds the airports

        var dep = new DateTime(2099, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        var arr = new DateTime(2099, 8, 1, 12, 0, 0, DateTimeKind.Utc);
        var csv = "FlightNumber,DepartureDate,ArrivalDate,DurationMinutes,OriginAirportCode,DestinationAirportCode,TotalCapacity\n" +
                  $"TKUP01," +
                  $"{dep.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}," +
                  $"{arr.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}," +
                  "120,CSVO,CSVD,180";

        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
        var form = new MultipartFormDataContent();
        form.Add(fileContent, "file", "flights.csv");

        var token = await GetAdminTokenAsync();
        var req   = Authorized(HttpMethod.Post, "/api/v1/flights/upload", token);
        req.Content = form;

        // Act
        var response = await _client.SendAsync(req);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        doc.RootElement.GetProperty("successCount").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("errors").GetArrayLength().Should().Be(0);
    }

    // ── Passenger manifest ────────────────────────────────────────────────────

    [Fact]
    public async Task GetPassengers_NoToken_Returns401()
    {
        // Act — no Authorization header
        var response = await _client.GetAsync("/api/v1/flights/TK001/date/2099-06-01/passengers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPassengers_AdminToken_Returns200WithItemsArray()
    {
        // Arrange — seed a flight so the endpoint resolves without error
        var flight = await SeedFlightAsync("SR10", "DS10", "SRCH10",
            new DateTime(2099, 11, 10, 10, 0, 0, DateTimeKind.Utc));

        var token   = await GetAdminTokenAsync();
        var request = Authorized(HttpMethod.Get,
            $"/api/v1/flights/{flight.FlightNumber}/date/2099-11-10/passengers", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetPassengers_InvalidDate_Returns400()
    {
        // Arrange
        var token   = await GetAdminTokenAsync();
        var request = Authorized(HttpMethod.Get,
            "/api/v1/flights/TK001/date/not-a-date/passengers", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert — controller parses date with TryParse → returns 400 on failure
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
