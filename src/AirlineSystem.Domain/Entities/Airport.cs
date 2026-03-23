namespace AirlineSystem.Domain.Entities;

public class Airport : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;

    public virtual ICollection<Flight> OutboundFlights { get; set; } = new List<Flight>();
    public virtual ICollection<Flight> InboundFlights { get; set; } = new List<Flight>();
}
