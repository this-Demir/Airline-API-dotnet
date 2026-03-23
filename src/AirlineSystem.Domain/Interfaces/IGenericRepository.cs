using AirlineSystem.Domain.Entities;

namespace AirlineSystem.Domain.Interfaces;

/// <summary>
/// Defines a generic, technology-agnostic repository contract for basic CRUD operations
/// against a persistent store. All mutating operations are staged and only committed
/// when <c>IUnitOfWork.SaveChangesAsync</c> is called.
/// </summary>
/// <typeparam name="T">A domain entity that inherits <see cref="BaseEntity"/>.</typeparam>
public interface IGenericRepository<T> where T : BaseEntity
{
    /// <summary>
    /// Retrieves a single entity by its primary key.
    /// </summary>
    /// <param name="id">The unique identifier (GUID) of the entity.</param>
    /// <returns>
    /// The matching entity, or <c>null</c> if no record with the given <paramref name="id"/> exists.
    /// </returns>
    Task<T?> GetByIdAsync(Guid id);

    /// <summary>
    /// Retrieves all entities of type <typeparamref name="T"/> from the store.
    /// </summary>
    /// <returns>
    /// An <see cref="IEnumerable{T}"/> containing every persisted record. Returns an empty
    /// collection — never <c>null</c> — when the table is empty.
    /// </returns>
    Task<IEnumerable<T>> GetAllAsync();

    /// <summary>
    /// Stages a new entity for insertion. The record is not persisted until
    /// <c>IUnitOfWork.SaveChangesAsync</c> is invoked.
    /// </summary>
    /// <param name="entity">The fully initialised entity to insert. Must not be <c>null</c>.</param>
    Task AddAsync(T entity);

    /// <summary>
    /// Marks an existing entity as modified. Changes are not written to the store until
    /// <c>IUnitOfWork.SaveChangesAsync</c> is invoked.
    /// </summary>
    /// <param name="entity">The entity containing the updated property values.</param>
    void Update(T entity);

    /// <summary>
    /// Marks an entity for deletion. The record is not removed from the store until
    /// <c>IUnitOfWork.SaveChangesAsync</c> is invoked.
    /// </summary>
    /// <param name="entity">The entity to delete. Must currently be tracked by the context.</param>
    void Delete(T entity);
}
