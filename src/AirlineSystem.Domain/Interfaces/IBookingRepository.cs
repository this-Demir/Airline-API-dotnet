using AirlineSystem.Domain.Entities;

namespace AirlineSystem.Domain.Interfaces;

public interface IBookingRepository : IGenericRepository<Booking>
{
    Task<Booking?> GetByPnrAsync(string pnrCode);
}
