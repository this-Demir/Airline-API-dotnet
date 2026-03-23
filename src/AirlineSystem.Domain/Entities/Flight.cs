namespace AirlineSystem.Domain.Entities;

public class Flight : BaseEntity
{
    public string FlightNumber { get; set; } = string.Empty;
    public DateTime DepartureDate { get; set; }
    public DateTime ArrivalDate { get; set; }
    public int DurationMinutes { get; set; }
    public int TotalCapacity { get; set; }
    public int AvailableCapacity { get; set; }
    public Guid OriginAirportId { get; set; }
    public Guid DestinationAirportId { get; set; }

    // Optimistic concurrency token — mapped via EF config in Infrastructure
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual Airport OriginAirport { get; set; } = null!;
    public virtual Airport DestinationAirport { get; set; } = null!;
    public virtual ICollection<Passenger> Passengers { get; set; } = new List<Passenger>();
}
