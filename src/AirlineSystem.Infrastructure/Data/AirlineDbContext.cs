using AirlineSystem.Domain.Entities;
using AirlineSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AirlineSystem.Infrastructure.Data;

/// <summary>
/// The EF Core database context for the Airline Ticketing System.
/// Owns all <see cref="DbSet{T}"/> collections and encapsulates every
/// model configuration decision that cannot be expressed with data annotations
/// alone — including concurrency tokens, string-enum conversions, unique indexes,
/// and the dual foreign-key relationship between <see cref="Flight"/> and
/// <see cref="Airport"/>.
/// </summary>
/// <remarks>
/// <b>Key model-configuration decisions documented here:</b>
/// <list type="bullet">
///   <item>
///     <b><see cref="User.Role"/> stored as string:</b> <c>.HasConversion&lt;string&gt;()</c>
///     is applied so the <see cref="UserRole"/> enum is persisted as a human-readable
///     string column (e.g., <c>"Admin"</c>, <c>"Customer"</c>) rather than its integer
///     ordinal. This is a confirmed architectural decision — changing it would corrupt
///     existing rows.
///   </item>
///   <item>
///     <b>Optimistic concurrency on <see cref="Flight.RowVersion"/>:</b>
///     <c>.IsRowVersion()</c> maps the <c>byte[]</c> property to a MySQL
///     <c>timestamp(6)</c> column that the database auto-updates on every write.
///     EF Core uses this column as the concurrency token when saving a
///     <see cref="Flight"/> entity; a stale-read conflict throws a
///     <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/>,
///     which the service layer catches to return a "sold out" result (FR-05.04).
///   </item>
///   <item>
///     <b>Dual FK from <see cref="Flight"/> to <see cref="Airport"/>:</b>
///     EF Core cannot auto-discover which navigation property corresponds to which
///     foreign key when two FKs point to the same principal table. Both relationships
///     are therefore configured explicitly with <c>HasOne / WithMany / HasForeignKey</c>
///     and <c>DeleteBehavior.Restrict</c> to prevent cascade-deletion of airports
///     that still have associated flights.
///   </item>
///   <item>
///     <b>Composite index on <c>(FlightNumber, DepartureDate)</c>:</b>
///     Required by the db_schema specification for high-performance flight search
///     and check-in lookups (see <c>IFlightRepository.SearchFlightsAsync</c> and
///     <c>IPassengerRepository.FindForCheckinAsync</c>).
///   </item>
/// </list>
/// </remarks>
public class AirlineDbContext : DbContext
{
    /// <inheritdoc cref="DbContext(DbContextOptions)"/>
    public AirlineDbContext(DbContextOptions<AirlineDbContext> options) : base(options) { }

    /// <summary>Gets or sets the <see cref="User"/> table.</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>Gets or sets the <see cref="Airport"/> table.</summary>
    public DbSet<Airport> Airports => Set<Airport>();

    /// <summary>Gets or sets the <see cref="Flight"/> table.</summary>
    public DbSet<Flight> Flights => Set<Flight>();

    /// <summary>Gets or sets the <see cref="Booking"/> table.</summary>
    public DbSet<Booking> Bookings => Set<Booking>();

    /// <summary>Gets or sets the <see cref="Passenger"/> table.</summary>
    public DbSet<Passenger> Passengers => Set<Passenger>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── User ────────────────────────────────────────────────────────────
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);

            entity.Property(u => u.Email)
                  .IsRequired()
                  .HasMaxLength(256);

            entity.HasIndex(u => u.Email)
                  .IsUnique();

            // Confirmed architectural decision: store enum as its string name, never as integer.
            entity.Property(u => u.Role)
                  .HasConversion<string>()
                  .IsRequired();
        });

        // ── Airport ─────────────────────────────────────────────────────────
        modelBuilder.Entity<Airport>(entity =>
        {
            entity.HasKey(a => a.Id);

            entity.Property(a => a.Code)
                  .IsRequired()
                  .HasMaxLength(10);

            entity.HasIndex(a => a.Code)
                  .IsUnique();

            entity.Property(a => a.Name).IsRequired();
            entity.Property(a => a.City).IsRequired();
        });

        // ── Flight ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Flight>(entity =>
        {
            entity.HasKey(f => f.Id);

            entity.Property(f => f.FlightNumber)
                  .IsRequired()
                  .HasMaxLength(20);

            // Optimistic concurrency token — Pomelo maps byte[] + IsRowVersion()
            // to a MySQL timestamp(6) column that auto-updates on every row write.
            entity.Property(f => f.RowVersion)
                  .IsRowVersion();

            // Composite index required by db_schema.md §3 and CLAUDE.md for search
            // and check-in performance.
            entity.HasIndex(f => new { f.FlightNumber, f.DepartureDate })
                  .HasDatabaseName("IX_Flights_FlightNumber_DepartureDate");

            // EF Core cannot auto-resolve two FK relationships to the same principal
            // table — both must be configured explicitly.
            entity.HasOne(f => f.OriginAirport)
                  .WithMany(a => a.OutboundFlights)
                  .HasForeignKey(f => f.OriginAirportId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(f => f.DestinationAirport)
                  .WithMany(a => a.InboundFlights)
                  .HasForeignKey(f => f.DestinationAirportId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Booking ─────────────────────────────────────────────────────────
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(b => b.Id);

            entity.Property(b => b.PnrCode)
                  .IsRequired()
                  .HasMaxLength(20);

            entity.HasIndex(b => b.PnrCode)
                  .IsUnique();

            entity.HasOne(b => b.User)
                  .WithMany(u => u.Bookings)
                  .HasForeignKey(b => b.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Passenger ────────────────────────────────────────────────────────
        modelBuilder.Entity<Passenger>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.Property(p => p.FullName).IsRequired();

            // SeatNumber is null until check-in is completed (FR-06.02).
            entity.Property(p => p.SeatNumber).IsRequired(false);

            entity.Property(p => p.IsCheckedIn)
                  .HasDefaultValue(false);

            // Intentional denormalization: both BookingId and FlightId are
            // always populated (confirmed architectural decision in CLAUDE.md).
            entity.HasOne(p => p.Booking)
                  .WithMany(b => b.Passengers)
                  .HasForeignKey(p => p.BookingId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(p => p.Flight)
                  .WithMany(f => f.Passengers)
                  .HasForeignKey(p => p.FlightId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
