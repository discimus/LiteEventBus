namespace LiteEventBus.Abstractions;

/// <summary>
/// Defines the contract for publishing events to registered subscribers.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publishes an event to all registered subscribers.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event to publish.</typeparam>
    /// <param name="event">The event instance to publish.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous publish operation.</returns>
    Task PublishAsync<TEvent>(
        TEvent @event,
        CancellationToken cancellationToken = default)
        where TEvent : IEvent;
}
