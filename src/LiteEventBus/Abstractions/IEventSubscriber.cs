namespace LiteEventBus.Abstractions;

/// <summary>
/// Defines the contract for a subscriber that handles events of type <typeparamref name="TEvent"/>.
/// </summary>
/// <typeparam name="TEvent">The type of event this subscriber handles.</typeparam>
public interface IEventSubscriber<in TEvent>
{
    /// <summary>
    /// Handles the specified event asynchronously.
    /// </summary>
    /// <param name="event">The event to handle.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous handle operation.</returns>
    Task HandleAsync(
        TEvent @event,
        CancellationToken cancellationToken);
}
