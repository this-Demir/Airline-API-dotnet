using AirlineSystem.Application.DTOs.CheckIn;

namespace AirlineSystem.Application.Interfaces;

public interface ICheckInService
{
    Task<CheckInResponseDto> CheckInPassengerAsync(CheckInRequestDto request);
}
