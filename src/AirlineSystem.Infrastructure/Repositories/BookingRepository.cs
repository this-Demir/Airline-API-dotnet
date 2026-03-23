using AirlineSystem.Domain.Entities;
using AirlineSystem.Domain.Interfaces;
using AirlineSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AirlineSystem.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IBookingRepository"/>.
/// Extends <see cref="GenericRepository{Booking}"/> with PNR-based
/// booking lookup.
/// </summary>
public class BookingRepository : GenericRepository<Booking>, IBookingRepository
{
    /// <summary>
    /// Initialises the repository with the shared database context.
    /// </summary>
    /// <param name="context">The EF Core context scoped to the current request.</param>
    public BookingRepository(AirlineDbContext context) : base(context) { }

    /// <inheritdoc/>
    /// <remarks>
    /// <b>_requires:</b> <paramref name="pnrCode"/> is a non-null, non-empty PNR string.
    /// <b>_ensures:</b> The <c>Passengers</c> collection is eagerly loaded so callers
    /// receive the full booking manifest without issuing a separate query. The
    /// <c>PnrCode</c> column carries a unique index (<c>AirlineDbContext</c>) so the
    /// lookup is O(log n).
    /// </remarks>
    public async Task<Booking?> GetByPnrAsync(string pnrCode) =>
        await _context.Bookings
            .Include(b => b.Passengers)
            .FirstOrDefaultAsync(b => b.PnrCode == pnrCode);
}
