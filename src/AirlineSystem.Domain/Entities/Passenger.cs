namespace AirlineSystem.Domain.Entities;

public class Passenger : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public int? SeatNumber { get; set; }
    public bool IsCheckedIn { get; set; }
    public Guid BookingId { get; set; }
    public Guid FlightId { get; set; }

    public virtual Booking Booking { get; set; } = null!;
    public virtual Flight Flight { get; set; } = null!;
}
