using AirlineSystem.Domain.Interfaces;

namespace AirlineSystem.Application.Interfaces;

/// <summary>
/// Coordinates multiple repository operations within a single atomic database transaction.
/// Implemented in the Infrastructure layer; services depend on this interface so they
/// remain free of EF Core and database concerns.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>Gets the repository for <c>User</c> entity operations.</summary>
    IUserRepository Users { get; }

    /// <summary>Gets the repository for <c>Airport</c> entity operations.</summary>
    IAirportRepository Airports { get; }

    /// <summary>Gets the repository for <c>Flight</c> entity operations.</summary>
    IFlightRepository Flights { get; }

    /// <summary>Gets the repository for <c>Booking</c> entity operations.</summary>
    IBookingRepository Bookings { get; }

    /// <summary>Gets the repository for <c>Passenger</c> entity operations.</summary>
    IPassengerRepository Passengers { get; }

    /// <summary>
    /// Commits all pending changes staged by the repositories to the database
    /// within a single transaction.
    /// </summary>
    /// <returns>
    /// The number of state entries written to the database.
    /// </returns>
    Task<int> SaveChangesAsync();
}
