using AirlineSystem.Domain.Interfaces;

namespace AirlineSystem.Application.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IUserRepository Users { get; }
    IAirportRepository Airports { get; }
    IFlightRepository Flights { get; }
    IBookingRepository Bookings { get; }
    IPassengerRepository Passengers { get; }
    Task<int> SaveChangesAsync();
}
