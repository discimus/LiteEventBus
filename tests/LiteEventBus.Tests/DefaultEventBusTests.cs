using LiteEventBus.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace LiteEventBus.Tests;

public class DefaultEventBusTests
{
    [Fact]
    public async Task PublishAsync_NoSubscribers_CompletesSuccessfully()
    {
        var services = new ServiceCollection();
        services.AddLiteEventBus();
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await eventBus.PublishAsync(new TestEvent());
    }

    [Fact]
    public async Task PublishAsync_OneSubscriber_CallsHandle()
    {
        var handler = new TestSubscriber();

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await eventBus.PublishAsync(new TestEvent());

        Assert.Equal(1, handler.HandleCount);
    }

    [Fact]
    public async Task PublishAsync_MultipleSubscribers_AllCalled()
    {
        var handler1 = new TestSubscriber();
        var handler2 = new TestSubscriber();

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler1);
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler2);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await eventBus.PublishAsync(new TestEvent());

        Assert.Equal(1, handler1.HandleCount);
        Assert.Equal(1, handler2.HandleCount);
    }

    [Fact]
    public async Task PublishAsync_MultipleSubscribers_ExecutesInRegistrationOrder()
    {
        var callOrder = new List<int>();
        var handler1 = new OrderedTestSubscriber(1, callOrder);
        var handler2 = new OrderedTestSubscriber(2, callOrder);

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler1);
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler2);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await eventBus.PublishAsync(new TestEvent());

        Assert.Equal([1, 2], callOrder);
    }

    [Fact]
    public async Task PublishAsync_SubscriberThrows_PropagatesException()
    {
        var handler1 = new TestSubscriber();
        var handler2 = new ThrowingTestSubscriber();

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler1);
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler2);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => eventBus.PublishAsync(new TestEvent()));
    }

    [Fact]
    public async Task PublishAsync_SubscriberThrows_SubsequentNotCalled()
    {
        var handler1 = new TestSubscriber();
        var throwingHandler = new ThrowingTestSubscriber();
        var handlerAfterThrow = new TestSubscriber();

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler1);
        services.AddSingleton<IEventSubscriber<TestEvent>>(throwingHandler);
        services.AddSingleton<IEventSubscriber<TestEvent>>(handlerAfterThrow);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        try
        {
            await eventBus.PublishAsync(new TestEvent());
        }
        catch (InvalidOperationException)
        {
        }

        Assert.Equal(1, handler1.HandleCount);
        Assert.Equal(0, handlerAfterThrow.HandleCount);
    }

    [Fact]
    public async Task PublishAsync_CancellationToken_ForwardedToSubscribers()
    {
        using var cts = new CancellationTokenSource();
        var handler = new CancellationTokenTestSubscriber();

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await eventBus.PublishAsync(new TestEvent(), cts.Token);

        Assert.True(handler.ReceivedToken.Equals(cts.Token));
    }

    [Fact]
    public async Task PublishAsync_MultipleEventTypes_IndependentDispatch()
    {
        var handlerA = new TestSubscriber();
        var handlerB = new AnotherEventTestSubscriber();

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(handlerA);
        services.AddSingleton<IEventSubscriber<AnotherTestEvent>>(handlerB);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await eventBus.PublishAsync(new TestEvent());
        await eventBus.PublishAsync(new AnotherTestEvent());

        Assert.Equal(1, handlerA.HandleCount);
        Assert.Equal(1, handlerB.HandleCount);
    }

    [Fact]
    public async Task PublishAsync_DI_ResolvesCorrectly()
    {
        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSubscriber<TestEvent, TestSubscriber>();
        var provider = services.BuildServiceProvider();

        var eventBus = provider.GetRequiredService<IEventBus>();
        var subscribers = provider.GetRequiredService<IEnumerable<IEventSubscriber<TestEvent>>>();

        Assert.NotNull(eventBus);
        Assert.Single(subscribers);
    }

    [Fact]
    public async Task PublishAsync_ConcurrentCalls_NoDeadlock()
    {
        var handler = new ConcurrentTestSubscriber();

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        var tasks = Enumerable.Range(0, 10).Select(_ =>
            eventBus.PublishAsync(new TestEvent()));

        await Task.WhenAll(tasks);

        Assert.Equal(10, handler.HandleCount);
    }

    [Fact]
    public async Task PublishAsync_NullEvent_ThrowsArgumentNullException()
    {
        var handler = new TestSubscriber();

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => eventBus.PublishAsync<TestEvent>(null!));
    }

    [Fact]
    public async Task PublishAsync_CancelledToken_ThrowsWhenSubscriberChecks()
    {
        var handler = new CancellationTokenCheckingSubscriber();

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => eventBus.PublishAsync(new TestEvent(), cts.Token));
    }

    [Fact]
    public async Task PublishAsync_NoSubscribersForEventType_EmptyEnumeration()
    {
        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(new TestSubscriber());
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await eventBus.PublishAsync(new AnotherTestEvent());
    }

    [Fact]
    public async Task PublishAsync_SubscriberThrows_ExceptionMessagePreserved()
    {
        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(new ThrowingTestSubscriber());
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => eventBus.PublishAsync(new TestEvent()));

        Assert.Equal("Test exception", ex.Message);
    }

    [Fact]
    public async Task PublishAsync_MultiplePublishes_SequentialCallsMaintainOrder()
    {
        var callOrder = new List<int>();
        var handler1 = new OrderedTestSubscriber(1, callOrder);
        var handler2 = new OrderedTestSubscriber(2, callOrder);

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler1);
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler2);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await eventBus.PublishAsync(new TestEvent());
        Assert.Equal([1, 2], callOrder);

        callOrder.Clear();

        await eventBus.PublishAsync(new TestEvent());
        Assert.Equal([1, 2], callOrder);
    }

    [Fact]
    public async Task PublishAsync_CancellationToken_CancelledDuringHandle_Propagates()
    {
        using var cts = new CancellationTokenSource();
        var handler = new CancellationDuringHandleSubscriber(cts);

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => eventBus.PublishAsync(new TestEvent(), cts.Token));
    }

    [Fact]
    public async Task PublishAsync_SubscriberReturnsCanceledTask_Propagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var handler = new PreCanceledSubscriber(cts.Token);

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => eventBus.PublishAsync(new TestEvent(), CancellationToken.None));
    }

    [Fact]
    public async Task PublishAsync_ResolvesTransientPerCall()
    {
        TransientTrackerSubscriber.Reset();

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddTransient<IEventSubscriber<TestEvent>, TransientTrackerSubscriber>();
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await eventBus.PublishAsync(new TestEvent());
        await eventBus.PublishAsync(new TestEvent());

        Assert.Equal(2, TransientTrackerSubscriber.InstanceCounter);
    }
}
