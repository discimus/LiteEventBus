# Basic Examples

## Defining Events

Events are plain .NET objects. Records are idiomatic for their value semantics and immutability, but any type works.

```csharp
public sealed record OrderSubmitted(
    Guid OrderId,
    string CustomerEmail,
    decimal Total);

public sealed record UserRegistered(
    Guid UserId,
    string Email,
    string FullName);
```

---

## Creating Class-Based Subscribers

Implement `IEventSubscriber<TEvent>` for each event type you want to handle.

```csharp
using LiteEventBus.Abstractions;

public sealed class SendOrderConfirmation : IEventSubscriber<OrderSubmitted>
{
    public Task HandleAsync(OrderSubmitted @event, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Confirmation sent to {@event.CustomerEmail}");
        return Task.CompletedTask;
    }
}

public sealed class UpdateInventory : IEventSubscriber<OrderSubmitted>
{
    public Task HandleAsync(OrderSubmitted @event, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Inventory updated for order {@event.OrderId}");
        return Task.CompletedTask;
    }
}
```

---

## Registering and Publishing

```csharp
using LiteEventBus.Abstractions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Register the event bus
services.AddLiteEventBus();

// Register subscribers
services.AddSubscriber<OrderSubmitted, SendOrderConfirmation>();
services.AddSubscriber<OrderSubmitted, UpdateInventory>();

var provider = services.BuildServiceProvider();
var eventBus = provider.GetRequiredService<IEventBus>();

// Publish
await eventBus.PublishAsync(new OrderSubmitted(
    Guid.NewGuid(),
    "customer@example.com",
    29.99m));
```

### Output

```
Confirmation sent to customer@example.com
Inventory updated for order 3a2f1c...
```

Subscribers execute in registration order: `SendOrderConfirmation` runs before `UpdateInventory`.

---

## Lambda Subscribers

For simple handlers, register a delegate instead of creating a class.

### With CancellationToken

```csharp
services.AddSubscriber<OrderSubmitted>(async (@event, ct) =>
{
    await EmailService.SendAsync(@event.CustomerEmail, ct);
});
```

### Without CancellationToken

```csharp
services.AddSubscriber<OrderSubmitted>(@event =>
{
    Console.WriteLine($"Order {@event.OrderId} received");
    return Task.CompletedTask;
});
```

### Synchronous

```csharp
services.AddSubscriber<OrderSubmitted>(@event =>
{
    Console.WriteLine($"Order total: {@event.Total:C}");
});
```

---

## Multiple Subscribers for the Same Event

Multiple subscribers for the same event type are supported. Each subscriber is invoked once per publish.

```csharp
services.AddSubscriber<OrderSubmitted, SendOrderConfirmation>();
services.AddSubscriber<OrderSubmitted, UpdateInventory>();
// Lambda as a third subscriber
services.AddSubscriber<OrderSubmitted>(@event =>
    Console.WriteLine($"Audit log: order {@event.OrderId}"));
```

---

## Multiple Event Types

Different event types are dispatched independently. Subscribers for `OrderSubmitted` do not receive `UserRegistered` events.

```csharp
services.AddSubscriber<OrderSubmitted, SendOrderConfirmation>();
services.AddSubscriber<UserRegistered, SendWelcomeEmail>();

var eventBus = provider.GetRequiredService<IEventBus>();

await eventBus.PublishAsync(new OrderSubmitted(...));
// Only SendOrderConfirmation runs

await eventBus.PublishAsync(new UserRegistered(...));
// Only SendWelcomeEmail runs
```

---

## Error Handling

### Default behavior (fail-fast)

```csharp
var services = new ServiceCollection();
services.AddLiteEventBus();
services.AddSubscriber<OrderSubmitted>(_ => throw new InvalidOperationException("Fail"));
services.AddSubscriber<OrderSubmitted>(_ => Console.WriteLine("Never runs"));

var provider = services.BuildServiceProvider();
var eventBus = provider.GetRequiredService<IEventBus>();

try
{
    await eventBus.PublishAsync(new OrderSubmitted(...));
}
catch (InvalidOperationException ex)
{
    Console.WriteLine(ex.Message); // "Fail"
}
// The second subscriber never executed.
```

### Continue-on-error

```csharp
var options = new PublishOptions { ContinueOnError = true };

try
{
    await eventBus.PublishAsync(new OrderSubmitted(...), options);
}
catch (AggregateException ex)
{
    foreach (var inner in ex.InnerExceptions)
    {
        Console.WriteLine($"Subscriber error: {inner.Message}");
    }
}
// All subscribers executed. Errors are collected.
```
