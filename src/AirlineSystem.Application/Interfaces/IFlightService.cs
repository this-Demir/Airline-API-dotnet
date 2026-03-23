using AirlineSystem.Application.DTOs.Flights;

namespace AirlineSystem.Application.Interfaces;

public interface IFlightService
{
    Task<PaginatedResultDto<FlightDto>> SearchFlightsAsync(FlightSearchRequestDto request);
    Task<(int SuccessCount, List<string> Errors)> UploadFlightsFromCsvAsync(Stream csvStream);
    Task<IEnumerable<FlightDto>> GetAllAsync();
    Task<FlightDto?> GetByIdAsync(Guid id);
    Task<FlightDto> CreateAsync(FlightUploadDto dto);
    Task UpdateAsync(Guid id, FlightUploadDto dto);
    Task DeleteAsync(Guid id);
}
