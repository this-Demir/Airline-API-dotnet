using AirlineSystem.Domain.Entities;

namespace AirlineSystem.Domain.Interfaces;

/// <summary>
/// Extends <see cref="IGenericRepository{Booking}"/> with PNR-based booking lookup.
/// </summary>
public interface IBookingRepository : IGenericRepository<Booking>
{
    /// <summary>
    /// Retrieves a booking by its unique Passenger Name Record (PNR) code.
    /// </summary>
    /// <remarks>
    /// The PNR code is the externally visible ticket number returned to the customer
    /// after a successful purchase. It is indexed for O(1) lookup performance.
    /// </remarks>
    /// <param name="pnrCode">The unique PNR code (e.g., <c>"A3F9B2"</c>).</param>
    /// <returns>
    /// The matching <see cref="Booking"/> entity including its <c>Passengers</c>
    /// collection, or <c>null</c> if no booking exists with the given PNR.
    /// </returns>
    Task<Booking?> GetByPnrAsync(string pnrCode);
}
