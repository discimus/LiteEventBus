using LiteEventBus.Abstractions;

namespace LiteEventBus.Tests;

internal sealed class TestSubscriber : IEventSubscriber<TestEvent>
{
    public int HandleCount { get; private set; }

    public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken)
    {
        HandleCount++;
        return Task.CompletedTask;
    }
}

internal sealed class OrderedTestSubscriber : IEventSubscriber<TestEvent>
{
    private readonly int _id;
    private readonly List<int> _callOrder;

    public OrderedTestSubscriber(int id, List<int> callOrder)
    {
        _id = id;
        _callOrder = callOrder;
    }

    public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken)
    {
        _callOrder.Add(_id);
        return Task.CompletedTask;
    }
}

internal sealed class ThrowingTestSubscriber : IEventSubscriber<TestEvent>
{
    public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Test exception");
    }
}

internal sealed class CancellationTokenTestSubscriber : IEventSubscriber<TestEvent>
{
    public CancellationToken ReceivedToken { get; private set; }

    public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken)
    {
        ReceivedToken = cancellationToken;
        return Task.CompletedTask;
    }
}

internal sealed class AnotherEventTestSubscriber : IEventSubscriber<AnotherTestEvent>
{
    public int HandleCount { get; private set; }

    public Task HandleAsync(AnotherTestEvent @event, CancellationToken cancellationToken)
    {
        HandleCount++;
        return Task.CompletedTask;
    }
}

internal sealed class ConcurrentTestSubscriber : IEventSubscriber<TestEvent>
{
    private int _handleCount;

    public int HandleCount => _handleCount;

    public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _handleCount);
        return Task.CompletedTask;
    }
}

internal sealed class CancellationDuringHandleSubscriber : IEventSubscriber<TestEvent>
{
    private readonly CancellationTokenSource _cts;

    public CancellationDuringHandleSubscriber(CancellationTokenSource cts)
    {
        _cts = cts;
    }

    public async Task HandleAsync(TestEvent @event, CancellationToken cancellationToken)
    {
        _cts.Cancel();
        await Task.Delay(Timeout.Infinite, cancellationToken);
    }
}

internal sealed class PreCanceledSubscriber : IEventSubscriber<TestEvent>
{
    private readonly CancellationToken _token;

    public PreCanceledSubscriber(CancellationToken token)
    {
        _token = token;
    }

    public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken)
    {
        return Task.FromCanceled(_token);
    }
}

internal sealed class CancellationTokenCheckingSubscriber : IEventSubscriber<TestEvent>
{
    public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

internal sealed class TransientTrackerSubscriber : IEventSubscriber<TestEvent>
{
    private static int _instanceCounter;

    public static int InstanceCounter => _instanceCounter;

    public TransientTrackerSubscriber()
    {
        Interlocked.Increment(ref _instanceCounter);
    }

    public static void Reset()
    {
        _instanceCounter = 0;
    }

    public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
