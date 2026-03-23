namespace AirlineSystem.Domain.Entities;

public class Booking : BaseEntity
{
    public string PnrCode { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public Guid UserId { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual ICollection<Passenger> Passengers { get; set; } = new List<Passenger>();
}
