using AirlineSystem.Domain.Entities;

namespace AirlineSystem.Domain.Interfaces;

public interface IUserRepository : IGenericRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
}
