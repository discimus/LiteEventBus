# Testing

## Unit Testing Subscribers

Subscribers are plain classes that implement `IEventSubscriber<TEvent>`. Test them directly.

```csharp
public sealed class OrderHandlerTests
{
    [Fact]
    public async Task HandleAsync_ValidEvent_AddsOrderToDatabase()
    {
        // Arrange
        var db = new AppDbContext(/* in-memory options */);
        var handler = new SaveOrderToDatabase(db);

        var @event = new OrderSubmitted(
            Guid.NewGuid(),
            "test@example.com",
            29.99m);

        // Act
        await handler.HandleAsync(@event, CancellationToken.None);

        // Assert
        Assert.Single(db.Orders);
        Assert.Equal(@event.OrderId, db.Orders.First().Id);
    }

    [Fact]
    public async Task HandleAsync_CancelledToken_Throws()
    {
        var db = new AppDbContext(/* in-memory options */);
        var handler = new SaveOrderToDatabase(db);
        var @event = new OrderSubmitted(Guid.NewGuid(), "test@example.com", 29.99m);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => handler.HandleAsync(@event, cts.Token));
    }
}
```

---

## Integration Testing with `IEventBus`

Test the full publish/subscribe pipeline with a real DI container.

### Test base setup

```csharp
public abstract class EventBusTestBase
{
    protected IServiceProvider CreateServiceProvider(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLiteEventBus();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    protected IEventBus CreateEventBus(Action<IServiceCollection>? configure = null)
    {
        var provider = CreateServiceProvider(configure);
        return provider.GetRequiredService<IEventBus>();
    }
}
```

### Publishing with no subscribers

```csharp
public class EventBusTests : EventBusTestBase
{
    [Fact]
    public async Task PublishAsync_NoSubscribers_Completes()
    {
        var eventBus = CreateEventBus();

        await eventBus.PublishAsync(new TestEvent());
        // No exception — publishing with zero subscribers is valid.
    }
}
```

### Subscriber invocation

```csharp
[Fact]
public async Task PublishAsync_Subscriber_CalledOnce()
{
    var handler = new TestSubscriber();
    var eventBus = CreateEventBus(services =>
    {
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler);
    });

    await eventBus.PublishAsync(new TestEvent());
    await eventBus.PublishAsync(new TestEvent());

    Assert.Equal(2, handler.HandleCount);
}
```

### Execution order

```csharp
[Fact]
public async Task PublishAsync_ExecutesInRegistrationOrder()
{
    var callOrder = new List<int>();
    var handler1 = new OrderedSubscriber(1, callOrder);
    var handler2 = new OrderedSubscriber(2, callOrder);

    var eventBus = CreateEventBus(services =>
    {
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler1);
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler2);
    });

    await eventBus.PublishAsync(new TestEvent());

    Assert.Equal([1, 2], callOrder);
}

private sealed class OrderedSubscriber(int id, List<int> callOrder) : IEventSubscriber<TestEvent>
{
    public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken)
    {
        callOrder.Add(id);
        return Task.CompletedTask;
    }
}
```

---

## Testing Error Handling

### Fail-fast

```csharp
[Fact]
public async Task PublishAsync_Exception_StopsExecution()
{
    var handler1 = new TestSubscriber();
    var failingHandler = new ThrowingSubscriber();
    var handlerAfter = new TestSubscriber();

    var eventBus = CreateEventBus(services =>
    {
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler1);
        services.AddSingleton<IEventSubscriber<TestEvent>>(failingHandler);
        services.AddSingleton<IEventSubscriber<TestEvent>>(handlerAfter);
    });

    await Assert.ThrowsAsync<InvalidOperationException>(
        () => eventBus.PublishAsync(new TestEvent()));

    Assert.Equal(1, handler1.HandleCount);
    Assert.Equal(0, handlerAfter.HandleCount); // Never called
}
```

### Continue-on-error

```csharp
[Fact]
public async Task PublishAsync_ContinueOnError_AllSubscribersCalled()
{
    var handler1 = new TestSubscriber();
    var failingHandler = new ThrowingSubscriber();
    var handler3 = new TestSubscriber();

    var eventBus = CreateEventBus(services =>
    {
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler1);
        services.AddSingleton<IEventSubscriber<TestEvent>>(failingHandler);
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler3);
    });

    var options = new PublishOptions { ContinueOnError = true };

    var ex = await Assert.ThrowsAsync<AggregateException>(
        () => eventBus.PublishAsync(new TestEvent(), options));

    Assert.Equal(1, handler1.HandleCount);
    Assert.Equal(1, handler3.HandleCount);
    Assert.Single(ex.InnerExceptions);
}
```

---

## Testing Lambda Subscribers

```csharp
[Fact]
public async Task PublishAsync_LambdaSubscriber_Called()
{
    var callCount = 0;

    var services = new ServiceCollection();
    services.AddLiteEventBus();
    services.AddSubscriber<TestEvent>((_, _) =>
    {
        callCount++;
        return Task.CompletedTask;
    });

    var provider = services.BuildServiceProvider();
    var eventBus = provider.GetRequiredService<IEventBus>();

    await eventBus.PublishAsync(new TestEvent());

    Assert.Equal(1, callCount);
}
```

---

## Mocking `IEventBus`

When testing consumers that depend on `IEventBus`, mock or stub the interface.

```csharp
public class OrderServiceTests
{
    [Fact]
    public async Task CreateOrder_PublishesEvent()
    {
        // Arrange
        var busMock = new Mock<IEventBus>();
        var service = new OrderService(busMock.Object);

        // Act
        await service.CreateOrderAsync("test@example.com", 29.99m);

        // Assert
        busMock.Verify(
            x => x.PublishAsync(
                It.IsAny<OrderSubmitted>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
```

---

## Test Doubles

```csharp
// Spy subscriber — records call count
internal sealed class TestSubscriber : IEventSubscriber<TestEvent>
{
    public int HandleCount { get; private set; }

    public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken)
    {
        HandleCount++;
        return Task.CompletedTask;
    }
}

// Stub that always throws
internal sealed class ThrowingSubscriber : IEventSubscriber<TestEvent>
{
    public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken)
        => throw new InvalidOperationException("Test exception");
}

// Test event
internal sealed record TestEvent;
```
