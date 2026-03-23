using AirlineSystem.Application.DTOs.CheckIn;
using AirlineSystem.Application.Interfaces;

namespace AirlineSystem.Application.Services;

public class CheckInService : ICheckInService
{
    private readonly IUnitOfWork _uow;

    public CheckInService(IUnitOfWork uow) => _uow = uow;

    public async Task<CheckInResponseDto> CheckInPassengerAsync(CheckInRequestDto request)
    {
        var passenger = await _uow.Passengers.FindForCheckinAsync(
            request.FlightNumber, request.Date, request.PassengerName);

        if (passenger is null)
            return new CheckInResponseDto
            {
                Status = "Failed",
                Message = "No ticket found for this passenger on this flight."
            };

        if (passenger.IsCheckedIn)
            return new CheckInResponseDto
            {
                Status = "Failed",
                Message = "Passenger has already checked in."
            };

        var nextSeat = await _uow.Passengers.GetNextSeatNumberAsync(passenger.FlightId);
        passenger.SeatNumber = nextSeat;
        passenger.IsCheckedIn = true;

        _uow.Passengers.Update(passenger);
        await _uow.SaveChangesAsync();

        return new CheckInResponseDto
        {
            Status = "Success",
            SeatNumber = nextSeat,
            FullName = passenger.FullName
        };
    }
}
