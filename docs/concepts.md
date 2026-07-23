# Core Concepts

## Events

An **event** is any plain .NET object that represents something that has happened in your application. Events are strongly typed — each event type defines its own data contract.

Events are not required to implement any interface or base class. Any .NET type (class, record, struct) is a valid event.

```csharp
public sealed record OrderSubmitted(
    Guid OrderId,
    string CustomerEmail,
    decimal Total);

public sealed record UserLoggedIn(
    Guid UserId,
    DateTime Timestamp);
```

Events are immutable data carriers. They should not contain behavior or logic.

---

## Subscribers

A **subscriber** (or handler) is responsible for reacting to an event. Subscribers implement `IEventSubscriber<TEvent>` and are registered in the DI container.

Subscribers are **transient** — a new instance is resolved for each publish operation. This allows subscribers to depend on scoped services (e.g., `DbContext`).

```csharp
public sealed class SendOrderConfirmation : IEventSubscriber<OrderSubmitted>
{
    private readonly IEmailService _email;

    public SendOrderConfirmation(IEmailService email)
    {
        _email = email;
    }

    public async Task HandleAsync(OrderSubmitted @event, CancellationToken cancellationToken)
    {
        await _email.SendAsync(@event.CustomerEmail, "Order Confirmed", cancellationToken);
    }
}
```

Subscribers can also be registered as **lambda delegates** without creating a dedicated class.

```csharp
services.AddSubscriber<OrderSubmitted>(async (@event, ct) =>
{
    await email.SendAsync(@event.CustomerEmail, "Order Confirmed", ct);
});
```

For lambda handlers that need to resolve dependencies from DI, use the overload with `IServiceProvider`:

```csharp
services.AddSubscriber<OrderSubmitted>((e, ct, sp) =>
{
    var db = sp.GetRequiredService<AppDbContext>();
    db.Orders.Add(new Order(e.OrderId, e.Total));
    return db.SaveChangesAsync(ct);
});
```

---

## Event Bus

The **event bus** (`IEventBus`) is the central service that coordinates publishing. It is registered as a **singleton** in the DI container.

When you call `PublishAsync`:

1. A new DI **scope** is created.
2. All subscribers registered for the event type are resolved from that scope.
3. Subscribers are executed **sequentially** in registration order.
4. The scope is disposed after all subscribers complete.

```csharp
await eventBus.PublishAsync(new OrderSubmitted(orderId, email, total));
```

---

## Publishing

Publishing is the act of sending an event to all registered subscribers. LiteEventBus provides two overloads:

```csharp
// Uses global defaults for error handling
Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default);

// Custom per-call options
Task PublishAsync<TEvent>(TEvent @event, PublishOptions options, CancellationToken ct = default);
```

---

## Error Handling Modes

### Fail-Fast (default)

The first exception thrown by a subscriber immediately stops execution. Subsequent subscribers are not invoked and the exception propagates directly.

```csharp
await eventBus.PublishAsync(new OrderSubmitted(...));
// If the 2nd subscriber throws, the 3rd subscriber never runs
```

### Continue-On-Error

When `ContinueOnError` is `true`, all subscribers execute regardless of exceptions. Exceptions are collected and an `AggregateException` is thrown after all subscribers complete.

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
        Console.WriteLine($"Subscriber failed: {inner.Message}");
    }
}
```

`OperationCanceledException` always propagates immediately regardless of this setting.

---

## DI Scopes

Each call to `PublishAsync` creates a new `IServiceScope`. This ensures that:

- Subscribers registered as **transient** get a fresh instance every time.
- Subscribers can inject **scoped** services (e.g., `DbContext`, `HttpContext`).
- Resources are properly disposed after the publish completes.

Scoped dependency example:

```csharp
public sealed class OrderHandler : IEventSubscriber<OrderSubmitted>
{
    private readonly AppDbContext _db;

    public OrderHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task HandleAsync(OrderSubmitted @event, CancellationToken ct)
    {
        _db.Orders.Add(new Order(@event.OrderId, @event.Total));
        await _db.SaveChangesAsync(ct);
    }
}
```

The subscriber depends on a scoped `DbContext`. Because each publish creates a new scope, this works correctly without requiring `IServiceScopeFactory` in the subscriber itself.

---

## Cancellation

All subscriber methods receive a `CancellationToken`. The token can be used to:

- Cancel long-running subscriber operations.
- Propagate shutdown signals to subscribers.
- Cooperatively cancel a publish operation.

When `CancellationToken` is cancelled before or during publishing:

- `OperationCanceledException` is thrown immediately.
- Remaining subscribers are **not** invoked.
- This behavior holds regardless of `ContinueOnError`.
