using AirlineSystem.Domain.Entities;

namespace AirlineSystem.Domain.Interfaces;

public interface IPassengerRepository : IGenericRepository<Passenger>
{
    Task<Passenger?> FindForCheckinAsync(string flightNumber, DateTime departureDate, string fullName);
    Task<int> GetNextSeatNumberAsync(Guid flightId);
    Task<(IEnumerable<Passenger> Items, int TotalCount)> GetFlightManifestAsync(
        string flightNumber,
        DateTime departureDate,
        int pageNumber);
}
