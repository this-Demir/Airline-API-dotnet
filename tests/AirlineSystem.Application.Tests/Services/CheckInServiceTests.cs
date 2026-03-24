using AirlineSystem.Application.DTOs.CheckIn;
using Xunit;
using AirlineSystem.Application.Interfaces;
using AirlineSystem.Application.Services;
using AirlineSystem.Domain.Entities;
using AirlineSystem.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace AirlineSystem.Application.Tests.Services;

public class CheckInServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUow = new();
    private readonly Mock<IBookingRepository> _mockBookingRepo = new();
    private readonly Mock<IPassengerRepository> _mockPassengerRepo = new();
    private readonly CheckInService _sut;

    public CheckInServiceTests()
    {
        _mockUow.Setup(u => u.Bookings).Returns(_mockBookingRepo.Object);
        _mockUow.Setup(u => u.Passengers).Returns(_mockPassengerRepo.Object);
        _mockUow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _sut = new CheckInService(_mockUow.Object);
    }

    private static CheckInRequestDto MakeRequest(string pnrCode = "ABC123", string name = "John Doe") =>
        new() { PnrCode = pnrCode, PassengerName = name };

    private static Booking MakeBooking(params Passenger[] passengers)
    {
        var booking = new Booking { Id = Guid.NewGuid(), PnrCode = "ABC123" };
        foreach (var p in passengers)
            booking.Passengers.Add(p);
        return booking;
    }

    // ── State Transition ────────────────────────────────────────────────────

    [Fact]
    public async Task CheckInPassengerAsync_PnrNotFound_ReturnsFailedStatus()
    {
        // S0 → S0: PNR does not exist in the database
        _mockBookingRepo
            .Setup(r => r.GetByPnrAsync(It.IsAny<string>()))
            .ReturnsAsync((Booking?)null);

        var result = await _sut.CheckInPassengerAsync(MakeRequest());

        result.Status.Should().Be("Failed");
        result.Message.Should().Contain("PNR");
        _mockUow.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task CheckInPassengerAsync_PassengerNotFoundInBooking_ReturnsFailedStatus()
    {
        // S0 → S0: PNR exists but the named passenger is not on this booking
        var booking = MakeBooking(new Passenger
        {
            Id = Guid.NewGuid(), FullName = "Jane Doe", FlightId = Guid.NewGuid(), IsCheckedIn = false
        });
        _mockBookingRepo
            .Setup(r => r.GetByPnrAsync("ABC123"))
            .ReturnsAsync(booking);

        var result = await _sut.CheckInPassengerAsync(MakeRequest(name: "Unknown Person"));

        result.Status.Should().Be("Failed");
        result.Message.Should().Contain("No ticket");
        _mockUow.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task CheckInPassengerAsync_ValidPassengerNotCheckedIn_ReturnsSuccessWithSeat()
    {
        // S1 → S2: passenger found, not yet checked in
        var flightId = Guid.NewGuid();
        var passenger = new Passenger
        {
            Id = Guid.NewGuid(), FullName = "John Doe", FlightId = flightId, IsCheckedIn = false
        };
        _mockBookingRepo
            .Setup(r => r.GetByPnrAsync("ABC123"))
            .ReturnsAsync(MakeBooking(passenger));
        _mockPassengerRepo
            .Setup(r => r.GetNextSeatNumberAsync(flightId))
            .ReturnsAsync(3);

        var result = await _sut.CheckInPassengerAsync(MakeRequest());

        result.Status.Should().Be("Success");
        result.SeatNumber.Should().Be(3);
        result.FullName.Should().Be("John Doe");
        passenger.IsCheckedIn.Should().BeTrue();
        passenger.SeatNumber.Should().Be(3);
        _mockUow.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CheckInPassengerAsync_PassengerAlreadyCheckedIn_ReturnsFailedStatus()
    {
        // S2 → S2: already checked in, must be rejected
        var passenger = new Passenger
        {
            Id = Guid.NewGuid(), FullName = "John Doe",
            FlightId = Guid.NewGuid(), IsCheckedIn = true, SeatNumber = 1
        };
        _mockBookingRepo
            .Setup(r => r.GetByPnrAsync("ABC123"))
            .ReturnsAsync(MakeBooking(passenger));

        var result = await _sut.CheckInPassengerAsync(MakeRequest());

        result.Status.Should().Be("Failed");
        result.Message.Should().Contain("already");
        _mockUow.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task CheckInPassengerAsync_Success_SeatAssignedFromGetNextSeatNumber()
    {
        // Verifies GetNextSeatNumberAsync result is propagated to the response
        var flightId = Guid.NewGuid();
        var passenger = new Passenger
        {
            Id = Guid.NewGuid(), FullName = "Jane Doe", FlightId = flightId, IsCheckedIn = false
        };
        _mockBookingRepo
            .Setup(r => r.GetByPnrAsync(It.IsAny<string>()))
            .ReturnsAsync(MakeBooking(passenger));
        _mockPassengerRepo
            .Setup(r => r.GetNextSeatNumberAsync(flightId))
            .ReturnsAsync(7);

        var result = await _sut.CheckInPassengerAsync(MakeRequest(name: "Jane Doe"));

        _mockPassengerRepo.Verify(r => r.GetNextSeatNumberAsync(flightId), Times.Once);
        result.SeatNumber.Should().Be(7);
        passenger.SeatNumber.Should().Be(7);
    }
}
