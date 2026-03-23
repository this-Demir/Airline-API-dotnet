using AirlineSystem.Domain.Entities;

namespace AirlineSystem.Domain.Interfaces;

public interface IFlightRepository : IGenericRepository<Flight>
{
    Task<Flight?> GetByFlightNumberAndDateAsync(string flightNumber, DateTime departureDate);
    Task<(IEnumerable<Flight> Items, int TotalCount)> SearchFlightsAsync(
        string originCode,
        string destinationCode,
        DateTime departureFrom,
        DateTime departureTo,
        int numberOfSeats,
        int pageNumber);
    Task<bool> ExistsAsync(string flightNumber, DateTime departureDate);
}
