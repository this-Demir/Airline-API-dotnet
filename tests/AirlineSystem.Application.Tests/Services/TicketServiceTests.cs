using AirlineSystem.Application.DTOs.Tickets;
using Xunit;
using AirlineSystem.Application.Interfaces;
using AirlineSystem.Application.Services;
using AirlineSystem.Domain.Entities;
using AirlineSystem.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace AirlineSystem.Application.Tests.Services;

public class TicketServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUow = new();
    private readonly Mock<IFlightRepository> _mockFlightRepo = new();
    private readonly Mock<IBookingRepository> _mockBookingRepo = new();
    private readonly TicketService _sut;

    private static readonly Guid UserId = Guid.NewGuid();

    public TicketServiceTests()
    {
        _mockUow.Setup(u => u.Flights).Returns(_mockFlightRepo.Object);
        _mockUow.Setup(u => u.Bookings).Returns(_mockBookingRepo.Object);
        _mockUow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _mockBookingRepo.Setup(r => r.AddAsync(It.IsAny<Booking>())).Returns(Task.CompletedTask);
        _sut = new TicketService(_mockUow.Object);
    }

    private static Flight MakeFlight(int capacity = 10, DateTime? departure = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            FlightNumber = "TK100",
            DepartureDate = (departure ?? DateTime.UtcNow.AddDays(5)).ToUniversalTime(),
            AvailableCapacity = capacity,
            TotalCapacity = capacity
        };

    private static BuyTicketRequestDto MakeRequest(int passengerCount = 2, DateTime? date = null) =>
        new()
        {
            FlightNumber = "TK100",
            FlightDate = date ?? DateTime.UtcNow.AddDays(5),
            PassengerNames = Enumerable.Range(1, passengerCount).Select(i => $"Passenger {i}").ToList()
        };

    // ── Equivalence Partitioning ────────────────────────────────────────────

    [Fact]
    public async Task BuyTicketAsync_ValidRequest_ReturnsConfirmedWithPnr()
    {
        // EP: valid partition — standard purchase
        var flight = MakeFlight(capacity: 10);
        _mockFlightRepo.Setup(r => r.GetByFlightNumberAndDateAsync("TK100", It.IsAny<DateTime>()))
            .ReturnsAsync(flight);

        var result = await _sut.BuyTicketAsync(MakeRequest(2), UserId);

        result.Status.Should().Be("Confirmed");
        result.PnrCode.Should().NotBeNullOrEmpty().And.HaveLength(6);
    }

    [Fact]
    public async Task BuyTicketAsync_ZeroPassengers_ThrowsArgumentException()
    {
        // EP: invalid partition — empty passenger list
        var request = MakeRequest(0);
        var act = () => _sut.BuyTicketAsync(request, UserId);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── Boundary Value Analysis ─────────────────────────────────────────────

    [Fact]
    public async Task BuyTicketAsync_CapacityExactlyEqualsPassengerCount_ReturnsConfirmed()
    {
        // BVA: boundary — capacity == requested, must succeed
        var flight = MakeFlight(capacity: 3);
        _mockFlightRepo.Setup(r => r.GetByFlightNumberAndDateAsync("TK100", It.IsAny<DateTime>()))
            .ReturnsAsync(flight);

        var result = await _sut.BuyTicketAsync(MakeRequest(3), UserId);

        result.Status.Should().Be("Confirmed");
    }

    [Fact]
    public async Task BuyTicketAsync_CapacityOneLessThanPassengerCount_ReturnsSoldOut()
    {
        // BVA: off-by-one — capacity is exactly 1 less than requested
        var flight = MakeFlight(capacity: 2);
        _mockFlightRepo.Setup(r => r.GetByFlightNumberAndDateAsync("TK100", It.IsAny<DateTime>()))
            .ReturnsAsync(flight);

        var result = await _sut.BuyTicketAsync(MakeRequest(3), UserId);

        result.Status.Should().Be("SoldOut");
        result.PnrCode.Should().BeNull();
    }

    // ── Error Guessing ──────────────────────────────────────────────────────

    [Fact]
    public async Task BuyTicketAsync_PastFlight_ThrowsInvalidOperationException()
    {
        // Error guessing: flight departed 1 day ago
        var pastDate = DateTime.UtcNow.AddDays(-1);
        var flight = MakeFlight(capacity: 10, departure: pastDate);
        _mockFlightRepo.Setup(r => r.GetByFlightNumberAndDateAsync("TK100", It.IsAny<DateTime>()))
            .ReturnsAsync(flight);

        var act = () => _sut.BuyTicketAsync(MakeRequest(1, pastDate), UserId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*departure date has already passed*");
    }

    [Fact]
    public async Task BuyTicketAsync_FlightNotFound_ThrowsKeyNotFoundException()
    {
        // Negative: flight does not exist
        _mockFlightRepo.Setup(r => r.GetByFlightNumberAndDateAsync("TK100", It.IsAny<DateTime>()))
            .ReturnsAsync((Flight?)null);

        var act = () => _sut.BuyTicketAsync(MakeRequest(1), UserId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── State Verification ──────────────────────────────────────────────────

    [Fact]
    public async Task BuyTicketAsync_Success_PassengerHasExplicitBookingIdAndFlightId()
    {
        // State: each Passenger must carry both BookingId and FlightId
        var flight = MakeFlight(capacity: 10);
        _mockFlightRepo.Setup(r => r.GetByFlightNumberAndDateAsync("TK100", It.IsAny<DateTime>()))
            .ReturnsAsync(flight);

        Booking? capturedBooking = null;
        _mockBookingRepo.Setup(r => r.AddAsync(It.IsAny<Booking>()))
            .Callback<Booking>(b => capturedBooking = b)
            .Returns(Task.CompletedTask);

        await _sut.BuyTicketAsync(MakeRequest(2), UserId);

        capturedBooking.Should().NotBeNull();
        capturedBooking!.Passengers.Should().HaveCount(2)
            .And.AllSatisfy(p =>
            {
                p.BookingId.Should().Be(capturedBooking.Id);
                p.FlightId.Should().Be(flight.Id);
            });
    }

    [Fact]
    public async Task BuyTicketAsync_Success_AvailableCapacityDecremented()
    {
        // State: AvailableCapacity reduced by number of passengers
        var flight = MakeFlight(capacity: 10);
        _mockFlightRepo.Setup(r => r.GetByFlightNumberAndDateAsync("TK100", It.IsAny<DateTime>()))
            .ReturnsAsync(flight);

        await _sut.BuyTicketAsync(MakeRequest(3), UserId);

        flight.AvailableCapacity.Should().Be(7);
        _mockFlightRepo.Verify(r => r.Update(flight), Times.Once);
    }

    [Fact]
    public async Task BuyTicketAsync_SoldOut_SaveChangesNeverCalled()
    {
        // State: no mutations when sold out
        var flight = MakeFlight(capacity: 1);
        _mockFlightRepo.Setup(r => r.GetByFlightNumberAndDateAsync("TK100", It.IsAny<DateTime>()))
            .ReturnsAsync(flight);

        await _sut.BuyTicketAsync(MakeRequest(5), UserId);

        _mockUow.Verify(u => u.SaveChangesAsync(), Times.Never);
    }
}
