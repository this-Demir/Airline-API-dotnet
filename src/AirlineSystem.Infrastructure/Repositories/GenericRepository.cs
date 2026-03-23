using AirlineSystem.Domain.Entities;
using AirlineSystem.Domain.Interfaces;
using AirlineSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AirlineSystem.Infrastructure.Repositories;

/// <summary>
/// Generic EF Core repository providing basic CRUD operations for any
/// <see cref="BaseEntity"/> subtype. All write operations stage changes in
/// the EF Core change tracker; nothing is persisted until
/// <c>IUnitOfWork.SaveChangesAsync</c> is called.
/// </summary>
/// <typeparam name="T">A domain entity that inherits <see cref="BaseEntity"/>.</typeparam>
public class GenericRepository<T> : IGenericRepository<T> where T : BaseEntity
{
    /// <summary>
    /// The shared <see cref="AirlineDbContext"/> instance injected by
    /// <see cref="UnitOfWork"/>. All repositories in the same Unit of Work
    /// share this context so their changes are tracked together and committed
    /// in a single database round-trip.
    /// </summary>
    protected readonly AirlineDbContext _context;

    /// <summary>
    /// Initialises the repository with the shared database context.
    /// </summary>
    /// <param name="context">The EF Core context scoped to the current request.</param>
    public GenericRepository(AirlineDbContext context) => _context = context;

    /// <inheritdoc/>
    /// <remarks>
    /// Delegates to <see cref="DbSet{TEntity}.FindAsync(object[])"/>, which checks
    /// the identity map (first-level cache) before hitting the database.
    /// </remarks>
    public async Task<T?> GetByIdAsync(Guid id) =>
        await _context.Set<T>().FindAsync(id);

    /// <inheritdoc/>
    /// <remarks>
    /// Issues a <c>SELECT *</c> against the table. Use only for small reference
    /// tables (e.g., Airport list for admin pages). For large tables prefer a
    /// filtered query in the concrete repository.
    /// </remarks>
    public async Task<IEnumerable<T>> GetAllAsync() =>
        await _context.Set<T>().ToListAsync();

    /// <inheritdoc/>
    /// <remarks>
    /// Calls <see cref="DbSet{TEntity}.AddAsync"/> which sets the entity state to
    /// <c>Added</c>. The INSERT SQL is generated and sent only when
    /// <c>SaveChangesAsync</c> is invoked on the owning <see cref="AirlineDbContext"/>.
    /// </remarks>
    public async Task AddAsync(T entity) =>
        await _context.Set<T>().AddAsync(entity);

    /// <inheritdoc/>
    /// <remarks>
    /// Calls <see cref="DbSet{TEntity}.Update"/> which sets the entity state to
    /// <c>Modified</c>. All scalar properties are included in the UPDATE statement.
    /// </remarks>
    public void Update(T entity) =>
        _context.Set<T>().Update(entity);

    /// <inheritdoc/>
    /// <remarks>
    /// Calls <see cref="DbSet{TEntity}.Remove"/> which sets the entity state to
    /// <c>Deleted</c>. The entity must be tracked by the context before calling
    /// this method; use <see cref="GetByIdAsync"/> first if needed.
    /// </remarks>
    public void Delete(T entity) =>
        _context.Set<T>().Remove(entity);
}
