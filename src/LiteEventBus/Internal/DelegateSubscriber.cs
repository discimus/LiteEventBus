using LiteEventBus.Abstractions;

namespace LiteEventBus.Internal;

internal sealed class DelegateSubscriber<TEvent> : IEventSubscriber<TEvent>
{
    private readonly Func<TEvent, CancellationToken, Task> _handler;

    public DelegateSubscriber(Func<TEvent, CancellationToken, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handler = handler;
    }

    public Task HandleAsync(TEvent @event, CancellationToken cancellationToken)
        => _handler(@event, cancellationToken);
}
