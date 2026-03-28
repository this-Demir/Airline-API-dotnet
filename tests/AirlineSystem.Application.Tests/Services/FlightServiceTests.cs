using System.Text;
using Xunit;
using AirlineSystem.Application.DTOs.Flights;
using AirlineSystem.Application.Interfaces;
using AirlineSystem.Application.Services;
using AirlineSystem.Domain.Entities;
using AirlineSystem.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace AirlineSystem.Application.Tests.Services;

public class FlightServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUow = new();
    private readonly Mock<IFlightRepository> _mockFlightRepo = new();
    private readonly Mock<IAirportRepository> _mockAirportRepo = new();
    private readonly FlightService _sut;

    public FlightServiceTests()
    {
        _mockUow.Setup(u => u.Flights).Returns(_mockFlightRepo.Object);
        _mockUow.Setup(u => u.Airports).Returns(_mockAirportRepo.Object);
        _mockUow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _mockFlightRepo.Setup(r => r.AddAsync(It.IsAny<Flight>())).Returns(Task.CompletedTask);
        _sut = new FlightService(_mockUow.Object);
    }

    private static Stream MakeCsvStream(string csv) =>
        new MemoryStream(Encoding.UTF8.GetBytes(csv));

    private static readonly string CsvHeader =
        "FlightNumber,DepartureDate,ArrivalDate,DurationMinutes,OriginAirportCode,DestinationAirportCode,TotalCapacity";

    // ── Complex Edge Case ───────────────────────────────────────────────────

    [Fact]
    public async Task UploadFlightsFromCsvAsync_MixedRows_OneSuccessThreeErrors()
    {
        /*
         * Row 1: TK100 — VALID
         * Row 2: TK200 — FAIL: DurationMinutes=999 vs actual 120
         * Row 3: TK300 — FAIL: ExistsAsync returns true (duplicate)
         * Row 4: TK400 — FAIL: Origin code "XXX" not found
         */
        var csv = $"{CsvHeader}\n" +
                  "TK100,2026-12-01 10:00,2026-12-01 12:00,120,IST,ADB,100\n" +
                  "TK200,2026-12-02 10:00,2026-12-02 12:00,999,IST,ADB,100\n" +
                  "TK300,2026-12-03 10:00,2026-12-03 12:00,120,IST,ADB,100\n" +
                  "TK400,2026-12-04 10:00,2026-12-04 12:00,120,XXX,ADB,100\n";

        var origin = new Airport { Id = Guid.NewGuid(), Code = "IST" };
        var dest   = new Airport { Id = Guid.NewGuid(), Code = "ADB" };

        _mockFlightRepo.Setup(r => r.ExistsAsync("TK100", It.IsAny<DateTime>())).ReturnsAsync(false);
        _mockFlightRepo.Setup(r => r.ExistsAsync("TK300", It.IsAny<DateTime>())).ReturnsAsync(true);

        _mockAirportRepo.Setup(r => r.GetByCodeAsync("IST")).ReturnsAsync(origin);
        _mockAirportRepo.Setup(r => r.GetByCodeAsync("ADB")).ReturnsAsync(dest);
        _mockAirportRepo.Setup(r => r.GetByCodeAsync("XXX")).ReturnsAsync((Airport?)null);

        var (successCount, errors) = await _sut.UploadFlightsFromCsvAsync(MakeCsvStream(csv));

        successCount.Should().Be(1);
        errors.Should().HaveCount(3);
        _mockFlightRepo.Verify(r => r.AddAsync(It.IsAny<Flight>()), Times.Once);
    }

    // ── Null / Empty Handling ───────────────────────────────────────────────

    [Fact]
    public async Task UploadFlightsFromCsvAsync_NullStream_ThrowsArgumentNullException()
    {
        // Null input guard
        var act = () => _sut.UploadFlightsFromCsvAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task UploadFlightsFromCsvAsync_EmptyCsv_ReturnsZeroSuccessAndNoErrors()
    {
        // Empty CSV (header only, no data rows)
        var csv = $"{CsvHeader}\n";

        var (successCount, errors) = await _sut.UploadFlightsFromCsvAsync(MakeCsvStream(csv));

        successCount.Should().Be(0);
        errors.Should().BeEmpty();
        _mockFlightRepo.Verify(r => r.AddAsync(It.IsAny<Flight>()), Times.Never);
        _mockUow.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    // ── Search / Delegation ─────────────────────────────────────────────────

    [Fact]
    public async Task SearchFlightsAsync_OneWay_DelegatesCorrectlyAndReturnsOutbound()
    {
        // Ensure capacity filtering parameter is forwarded and pagination is wrapped correctly
        var futureFlight = new Flight
        {
            Id = Guid.NewGuid(),
            FlightNumber = "TK999",
            DepartureDate = DateTime.UtcNow.AddDays(10),
            ArrivalDate = DateTime.UtcNow.AddDays(10).AddHours(2),
            AvailableCapacity = 5,
            OriginAirport = new Airport { Code = "IST" },
            DestinationAirport = new Airport { Code = "ADB" }
        };

        _mockFlightRepo
            .Setup(r => r.SearchFlightsAsync("IST", "ADB", It.IsAny<DateTime>(), It.IsAny<DateTime>(), 2, 1))
            .ReturnsAsync((new[] { futureFlight }, 1));

        var request = new FlightSearchRequestDto
        {
            OriginCode = "IST",
            DestinationCode = "ADB",
            DepartureFrom = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            DepartureTo = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd"),
            NumberOfPeople = 2,
            PageNumber = 1
        };

        var result = await _sut.SearchFlightsAsync(request);

        result.Outbound.Items.Should().HaveCount(1);
        result.Outbound.TotalCount.Should().Be(1);
        result.Outbound.PageNumber.Should().Be(1);
        result.ReturnFlights.Should().BeNull();
        _mockFlightRepo.Verify(
            r => r.SearchFlightsAsync("IST", "ADB", It.IsAny<DateTime>(), It.IsAny<DateTime>(), 2, 1),
            Times.Once);
    }

    [Fact]
    public async Task SearchFlightsAsync_RoundTrip_RunsTwoQueriesAndPopulatesReturnFlights()
    {
        // S0: round-trip request with IST→ADB
        // S1: service runs outbound query (IST→ADB) and return query (ADB→IST)
        var outboundFlight = new Flight
        {
            Id = Guid.NewGuid(), FlightNumber = "TK100",
            DepartureDate = DateTime.UtcNow.AddDays(10),
            ArrivalDate = DateTime.UtcNow.AddDays(10).AddHours(2),
            AvailableCapacity = 10,
            OriginAirport = new Airport { Code = "IST" },
            DestinationAirport = new Airport { Code = "ADB" }
        };
        var returnFlight = new Flight
        {
            Id = Guid.NewGuid(), FlightNumber = "TK200",
            DepartureDate = DateTime.UtcNow.AddDays(17),
            ArrivalDate = DateTime.UtcNow.AddDays(17).AddHours(2),
            AvailableCapacity = 10,
            OriginAirport = new Airport { Code = "ADB" },
            DestinationAirport = new Airport { Code = "IST" }
        };

        _mockFlightRepo
            .Setup(r => r.SearchFlightsAsync("IST", "ADB", It.IsAny<DateTime>(), It.IsAny<DateTime>(), 2, 1))
            .ReturnsAsync((new[] { outboundFlight }, 1));
        _mockFlightRepo
            .Setup(r => r.SearchFlightsAsync("ADB", "IST", It.IsAny<DateTime>(), It.IsAny<DateTime>(), 2, 1))
            .ReturnsAsync((new[] { returnFlight }, 1));

        var request = new FlightSearchRequestDto
        {
            OriginCode = "IST",
            DestinationCode = "ADB",
            DepartureFrom = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            DepartureTo = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd"),
            NumberOfPeople = 2,
            PageNumber = 1,
            IsRoundTrip = true
        };

        var result = await _sut.SearchFlightsAsync(request);

        result.Outbound.Items.Should().HaveCount(1);
        result.ReturnFlights.Should().NotBeNull();
        result.ReturnFlights!.Items.Should().HaveCount(1);
        _mockFlightRepo.Verify(
            r => r.SearchFlightsAsync(It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), 2, 1),
            Times.Exactly(2));
    }
}
