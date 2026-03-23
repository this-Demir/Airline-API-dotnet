using AirlineSystem.Domain.Enums;

namespace AirlineSystem.Domain.Entities;

public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
