namespace AirlineSystem.Application.Exceptions;

/// <summary>
/// Thrown when a write operation fails due to an optimistic concurrency conflict
/// and all retry attempts have been exhausted.
/// </summary>
public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
        : base("A concurrency conflict occurred. Please retry the request.") { }
}
