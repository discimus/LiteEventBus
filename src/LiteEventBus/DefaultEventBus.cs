using LiteEventBus.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace LiteEventBus.Internal;

internal sealed class DefaultEventBus : IEventBus
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EventBusOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultEventBus"/> class.
    /// </summary>
    /// <param name="scopeFactory">The service scope factory used to create scopes for each publish.</param>
    /// <param name="options">The global options for the event bus.</param>
    public DefaultEventBus(IServiceScopeFactory scopeFactory, EventBusOptions options)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(options);
        _scopeFactory = scopeFactory;
        _options = options;
    }

    /// <summary>
    /// Publishes an event to all registered subscribers, executing them sequentially.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event to publish.</typeparam>
    /// <param name="event">The event instance to publish.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous publish operation.</returns>
    public Task PublishAsync<TEvent>(
        TEvent @event,
        CancellationToken cancellationToken = default)
        where TEvent : IEvent
    {
        return PublishAsync(@event, options: null, cancellationToken);
    }

    /// <summary>
    /// Publishes an event to all registered subscribers with the specified options.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event to publish.</typeparam>
    /// <param name="event">The event instance to publish.</param>
    /// <param name="options">Options that control the publish behavior. When <see langword="null"/>,
    /// the global defaults from <see cref="EventBusOptions"/> are used.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous publish operation.</returns>
    public async Task PublishAsync<TEvent>(
        TEvent @event,
        PublishOptions? options,
        CancellationToken cancellationToken = default)
        where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(@event);

        using var scope = _scopeFactory.CreateScope();
        var subscribers = scope.ServiceProvider.GetRequiredService<IEnumerable<IEventSubscriber<TEvent>>>();

        options ??= new PublishOptions { ContinueOnError = _options.DefaultContinueOnError };

        var exceptions = new List<Exception>();

        foreach (var subscriber in subscribers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await subscriber.HandleAsync(@event, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (options.ContinueOnError)
            {
                exceptions.Add(ex);

                if (_options.OnSubscriberError is not null)
                {
                    await _options.OnSubscriberError
                        .Invoke(scope.ServiceProvider, @event, ex)
                        .ConfigureAwait(false);
                }
            }
        }

        if (exceptions.Count > 0)
        {
            throw new AggregateException(exceptions);
        }
    }
}
