using LiteEventBus.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace LiteEventBus.Internal;

internal sealed class DefaultEventBus : IEventBus
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultEventBus"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve subscribers.</param>
    public DefaultEventBus(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Publishes an event to all registered subscribers, executing them sequentially.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event to publish.</typeparam>
    /// <param name="event">The event instance to publish.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous publish operation.</returns>
    public async Task PublishAsync<TEvent>(
        TEvent @event,
        CancellationToken cancellationToken = default)
        where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(@event);

        var subscribers = _serviceProvider.GetRequiredService<IEnumerable<IEventSubscriber<TEvent>>>();

        foreach (var subscriber in subscribers)
        {
            await subscriber.HandleAsync(@event, cancellationToken).ConfigureAwait(false);
        }
    }
}
