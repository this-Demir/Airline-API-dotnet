using AirlineSystem.Application.Exceptions;
using AirlineSystem.Application.Interfaces;
using AirlineSystem.Domain.Interfaces;
using AirlineSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AirlineSystem.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IUnitOfWork"/>. Coordinates all five
/// domain repositories over a single shared <see cref="AirlineDbContext"/> so
/// that every change staged within a request is committed atomically in one
/// database transaction.
/// </summary>
/// <remarks>
/// <b>Why a shared context matters:</b> All repositories receive the same
/// <see cref="AirlineDbContext"/> instance. This means EF Core's identity map
/// (first-level cache) is shared: if <c>FlightRepository</c> loads a
/// <see cref="Domain.Entities.Flight"/> and <c>PassengerRepository</c> later
/// accesses the same row, EF Core returns the already-tracked instance — no
/// duplicate SQL. More critically, all <c>Update / Add / Remove</c> calls across
/// different repositories are batched into a single <c>SaveChangesAsync</c> call,
/// which executes within one implicit database transaction (EF Core default).
/// <para>
/// <b>Lifetime:</b> Register <see cref="UnitOfWork"/> as <c>Scoped</c> in the DI
/// container so one instance (and one <see cref="AirlineDbContext"/>) is created
/// per HTTP request and disposed at the end of the request.
/// </para>
/// </remarks>
public class UnitOfWork : IUnitOfWork
{
    private readonly AirlineDbContext _context;

    /// <summary>
    /// Initialises the Unit of Work and all five domain repositories.
    /// </summary>
    /// <param name="context">
    /// The EF Core context scoped to the current request. All repositories share
    /// this instance.
    /// </param>
    public UnitOfWork(AirlineDbContext context)
    {
        _context = context;
        Users = new UserRepository(context);
        Airports = new AirportRepository(context);
        Flights = new FlightRepository(context);
        Bookings = new BookingRepository(context);
        Passengers = new PassengerRepository(context);
    }

    /// <inheritdoc/>
    public IUserRepository Users { get; }

    /// <inheritdoc/>
    public IAirportRepository Airports { get; }

    /// <inheritdoc/>
    public IFlightRepository Flights { get; }

    /// <inheritdoc/>
    public IBookingRepository Bookings { get; }

    /// <inheritdoc/>
    public IPassengerRepository Passengers { get; }

    /// <inheritdoc/>
    /// <remarks>
    /// Delegates to <see cref="Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync(System.Threading.CancellationToken)"/>,
    /// which flushes all pending <c>Added / Modified / Deleted</c> entity states
    /// to the database within a single implicit transaction.
    /// <para>
    /// Any <see cref="DbUpdateConcurrencyException"/> (stale <c>RowVersion</c>) is
    /// wrapped as a <see cref="ConcurrencyConflictException"/> so callers remain
    /// free of EF Core dependencies.
    /// </para>
    /// </remarks>
    public async Task<int> SaveChangesAsync()
    {
        try
        {
            return await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException();
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Calls <c>DbContext.Entry(entity).ReloadAsync()</c>, which issues a fresh
    /// <c>SELECT</c> and overwrites all tracked property values, including the
    /// <c>RowVersion</c> concurrency token, then resets entity state to <c>Unchanged</c>.
    /// </remarks>
    public async Task ReloadEntityAsync<T>(T entity) where T : class =>
        await _context.Entry(entity).ReloadAsync();

    /// <inheritdoc/>
    public void Dispose() => _context.Dispose();
}
