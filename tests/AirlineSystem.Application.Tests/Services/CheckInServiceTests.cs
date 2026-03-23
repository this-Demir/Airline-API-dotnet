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
    private readonly Mock<IPassengerRepository> _mockPassengerRepo = new();
    private readonly CheckInService _sut;

    public CheckInServiceTests()
    {
        _mockUow.Setup(u => u.Passengers).Returns(_mockPassengerRepo.Object);
        _mockUow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _sut = new CheckInService(_mockUow.Object);
    }

    private static CheckInRequestDto MakeRequest(string flightNumber = "TK100") =>
        new() { FlightNumber = flightNumber, Date = DateTime.UtcNow.AddDays(1), PassengerName = "John Doe" };

    // ── State Transition ────────────────────────────────────────────────────

    [Fact]
    public async Task CheckInPassengerAsync_PassengerNotFound_ReturnsFailedStatus()
    {
        // S0 → S0: no Passenger record exists
        _mockPassengerRepo
            .Setup(r => r.FindForCheckinAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>()))
            .ReturnsAsync((Passenger?)null);

        var result = await _sut.CheckInPassengerAsync(MakeRequest());

        result.Status.Should().Be("Failed");
        result.Message.Should().Contain("No ticket");
        _mockUow.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task CheckInPassengerAsync_ValidPassengerNotCheckedIn_ReturnsSuccessWithSeat()
    {
        // S1 → S2: passenger exists, not yet checked in
        var flightId = Guid.NewGuid();
        var passenger = new Passenger
        {
            Id = Guid.NewGuid(),
            FullName = "John Doe",
            FlightId = flightId,
            IsCheckedIn = false
        };
        _mockPassengerRepo
            .Setup(r => r.FindForCheckinAsync(It.IsAny<string>(), It.IsAny<DateTime>(), "John Doe"))
            .ReturnsAsync(passenger);
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
            Id = Guid.NewGuid(),
            FullName = "John Doe",
            FlightId = Guid.NewGuid(),
            IsCheckedIn = true,
            SeatNumber = 1
        };
        _mockPassengerRepo
            .Setup(r => r.FindForCheckinAsync(It.IsAny<string>(), It.IsAny<DateTime>(), "John Doe"))
            .ReturnsAsync(passenger);

        var result = await _sut.CheckInPassengerAsync(MakeRequest());

        result.Status.Should().Be("Failed");
        result.Message.Should().Contain("already");
        _mockUow.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task CheckInPassengerAsync_Success_SeatAssignedFromGetNextSeatNumber()
    {
        // Concurrency simulation: verifies GetNextSeatNumberAsync result is used
        var flightId = Guid.NewGuid();
        var passenger = new Passenger
        {
            Id = Guid.NewGuid(),
            FullName = "Jane Doe",
            FlightId = flightId,
            IsCheckedIn = false
        };
        _mockPassengerRepo
            .Setup(r => r.FindForCheckinAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>()))
            .ReturnsAsync(passenger);
        _mockPassengerRepo
            .Setup(r => r.GetNextSeatNumberAsync(flightId))
            .ReturnsAsync(7);

        var result = await _sut.CheckInPassengerAsync(MakeRequest());

        _mockPassengerRepo.Verify(r => r.GetNextSeatNumberAsync(flightId), Times.Once);
        result.SeatNumber.Should().Be(7);
        passenger.SeatNumber.Should().Be(7);
    }
}
