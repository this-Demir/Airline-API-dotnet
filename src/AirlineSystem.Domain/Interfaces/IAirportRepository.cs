using AirlineSystem.Domain.Entities;

namespace AirlineSystem.Domain.Interfaces;

public interface IAirportRepository : IGenericRepository<Airport>
{
    Task<Airport?> GetByCodeAsync(string code);
}
