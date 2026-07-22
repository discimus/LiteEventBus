# Advanced Examples

## Scoped Dependencies (EF Core)

Each `PublishAsync` call creates a DI scope. Subscribers can inject scoped services like `DbContext`.

```csharp
// Event
public sealed record OrderSubmitted(
    Guid OrderId,
    string CustomerEmail,
    decimal Total);

// Subscriber with scoped dependency
public sealed class SaveOrderToDatabase : IEventSubscriber<OrderSubmitted>
{
    private readonly AppDbContext _db;

    public SaveOrderToDatabase(AppDbContext db)
    {
        _db = db;
    }

    public async Task HandleAsync(OrderSubmitted @event, CancellationToken cancellationToken)
    {
        _db.Orders.Add(new Order
        {
            Id = @event.OrderId,
            CustomerEmail = @event.CustomerEmail,
            Total = @event.Total,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
    }
}
```

Registration:

```csharp
services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

services.AddLiteEventBus();
services.AddSubscriber<OrderSubmitted, SaveOrderToDatabase>();
```

Each publish gets a fresh scope, so `AppDbContext` is correctly resolved as scoped within that scope.

---

## Transient Subscribers Per Publish

Subscribers are resolved as **transient** every time `PublishAsync` is called. This means each publish gets a new instance even if the subscriber has no scoped dependencies.

```csharp
public sealed class TrackingSubscriber : IEventSubscriber<OrderSubmitted>
{
    private static int _instanceCounter;

    public TrackingSubscriber()
    {
        Interlocked.Increment(ref _instanceCounter);
    }

    public static int InstanceCount => _instanceCounter;

    public Task HandleAsync(OrderSubmitted @event, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
```

```csharp
services.AddLiteEventBus();
services.AddTransient<IEventSubscriber<OrderSubmitted>, TrackingSubscriber>();

var provider = services.BuildServiceProvider();
var eventBus = provider.GetRequiredService<IEventBus>();

await eventBus.PublishAsync(new OrderSubmitted(...));
await eventBus.PublishAsync(new OrderSubmitted(...));

Console.WriteLine(TrackingSubscriber.InstanceCount); // 2
```

---

## Multiple Subscribers for the Same Event (Mixed Class + Lambda)

Class-based and lambda subscribers can coexist for the same event type.

```csharp
services.AddLiteEventBus();
services.AddSubscriber<OrderSubmitted, SendOrderConfirmation>();
services.AddSubscriber<OrderSubmitted>(@event =>
    Console.WriteLine($"[Lambda] Order {@event.OrderId} logged"));

var eventBus = provider.GetRequiredService<IEventBus>();

await eventBus.PublishAsync(new OrderSubmitted(...));
// Both the class subscriber and the lambda subscriber run.
```

---

## Concurrent Publishing

Concurrent publishes of the same or different event types are safe. Each call creates an independent scope.

```csharp
var tasks = Enumerable.Range(0, 10).Select(_ =>
    eventBus.PublishAsync(new OrderSubmitted(...)));

await Task.WhenAll(tasks);

// All 10 publishes completed concurrently.
// Each subscriber invocation was isolated in its own scope.
```

---

## Error Handling with Callback

Combine `ContinueOnError` with `OnSubscriberError` for resilient event processing with diagnostics.

```csharp
services.AddLiteEventBus(options =>
{
    options.DefaultContinueOnError = true;
    options.OnSubscriberError = async (sp, @event, exception) =>
    {
        var logger = sp.GetRequiredService<ILogger<IEventBus>>();
        logger.LogError(exception,
            "Subscriber failed processing {EventType}: {ErrorMessage}",
            @event.GetType().Name,
            exception.Message);

        // Optionally send to a dead-letter queue or monitoring system
        var dlq = sp.GetRequiredService<IDeadLetterService>();
        await dlq.PublishAsync(@event, exception);
    };
});
```

---

## Overriding Error Behavior Per-Publish

Even when global default is `ContinueOnError = true`, you can override per-publish.

```csharp
services.AddLiteEventBus(options =>
{
    options.DefaultContinueOnError = true;
});

// ...

// This publish uses ContinueOnError = false (fail-fast)
await eventBus.PublishAsync(
    new CriticalPaymentEvent(...),
    new PublishOptions { ContinueOnError = false });
```

---

## Subscriber with External Dependencies

Subscribers resolve all dependencies via DI. No service locator required.

```csharp
public sealed class OrderSubmittedHandler : IEventSubscriber<OrderSubmitted>
{
    private readonly AppDbContext _db;
    private readonly IEmailService _email;
    private readonly ILogger<OrderSubmittedHandler> _logger;

    public OrderSubmittedHandler(
        AppDbContext db,
        IEmailService email,
        ILogger<OrderSubmittedHandler> logger)
    {
        _db = db;
        _email = email;
        _logger = logger;
    }

    public async Task HandleAsync(OrderSubmitted @event, CancellationToken ct)
    {
        _logger.LogInformation("Processing order {OrderId}", @event.OrderId);

        _db.Orders.Add(new Order { Id = @event.OrderId, Total = @event.Total });
        await _db.SaveChangesAsync(ct);

        await _email.SendAsync(@event.CustomerEmail, "Order Confirmed", ct);

        _logger.LogInformation("Order {OrderId} processed", @event.OrderId);
    }
}
```
