# Extensibility

## Overview

LiteEventBus is intentionally minimal but provides several extension points for customization.

---

## 1. Custom Subscriber Implementations

Any class that implements `IEventSubscriber<TEvent>` is a valid subscriber. There are no additional constraints.

```csharp
public sealed class LoggingSubscriber<TEvent> : IEventSubscriber<TEvent>
{
    private readonly ILogger<LoggingSubscriber<TEvent>> _logger;
    private readonly IEventSubscriber<TEvent> _inner;

    public LoggingSubscriber(
        ILogger<LoggingSubscriber<TEvent>> logger,
        IEventSubscriber<TEvent> inner)
    {
        _logger = logger;
        _inner = inner;
    }

    public async Task HandleAsync(TEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling {EventType}", typeof(TEvent).Name);

        try
        {
            await _inner.HandleAsync(@event, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling {EventType}", typeof(TEvent).Name);
            throw;
        }
    }
}
```

Register via DI:

```csharp
services.AddScoped<LoggingSubscriber<OrderSubmitted>>();
services.AddTransient<IEventSubscriber<OrderSubmitted>>(sp =>
    ActivatorUtilities.CreateInstance<LoggingSubscriber<OrderSubmitted>>(sp,
        sp.GetRequiredService<RealOrderHandler>()));
```

---

## 2. Lambda Subscribers

The simplest extension point. Register handlers inline without creating a class.

```csharp
// Inline handler with full async support
services.AddSubscriber<OrderSubmitted>(async (@event, ct) =>
{
    await SendEmailAsync(@event.CustomerEmail, ct);
    await LogToDatabaseAsync(@event, ct);
});

// Sync handler
services.AddSubscriber<OrderSubmitted>(@event =>
    Console.WriteLine($"Received order {@event.OrderId}"));
```

---

## 3. Error Callback (`OnSubscriberError`)

React to subscriber failures for logging, metrics, or dead-letter queues.

```csharp
services.AddLiteEventBus(options =>
{
    options.DefaultContinueOnError = true;
    options.OnSubscriberError = async (sp, @event, exception) =>
    {
        var metrics = sp.GetRequiredService<IMetricsCollector>();
        metrics.Increment("eventbus.subscriber.errors",
            new Tag("event_type", @event.GetType().Name));

        var dlq = sp.GetRequiredService<IDeadLetterService>();
        await dlq.PublishAsync(@event, exception);
    };
});
```

The callback receives:

| Parameter | Type | Description |
|-----------|------|-------------|
| `sp` | `IServiceProvider` | Scope-level provider — can resolve scoped services |
| `@event` | `object` | The event that caused the failure |
| `exception` | `Exception` | The exception thrown by the subscriber |

**Note:** If the callback itself throws, the exception is swallowed to avoid breaking the publish flow.

---

## 4. Decorating `IEventBus`

Wrap `IEventBus` to add cross-cutting concerns like metrics, tracing, or retry logic.

```csharp
public sealed class MetricsEventBus : IEventBus
{
    private readonly IEventBus _inner;
    private readonly IMeterFactory _meterFactory;

    public MetricsEventBus(IEventBus inner, IMeterFactory meterFactory)
    {
        _inner = inner;
        _meterFactory = meterFactory;
    }

    public async Task PublishAsync<TEvent>(
        TEvent @event,
        CancellationToken cancellationToken = default)
    {
        var counter = _meterFactory.Create("eventbus.publish.count");
        counter.Add(1);

        var sw = ValueStopwatch.StartNew();

        try
        {
            await _inner.PublishAsync(@event, cancellationToken);
        }
        finally
        {
            var histogram = _meterFactory.Create("eventbus.publish.duration");
            histogram.Record(sw.Elapsed.TotalMilliseconds);
        }
    }

    public async Task PublishAsync<TEvent>(
        TEvent @event,
        PublishOptions options,
        CancellationToken cancellationToken = default)
    {
        await _inner.PublishAsync(@event, options, cancellationToken);
    }
}
```

Registration:

```csharp
services.AddLiteEventBus();

// Decorate IEventBus
services.Decorate<IEventBus, MetricsEventBus>();
```

> **Note:** Decorator pattern requires a DI container that supports decoration (e.g., Scrutor or manual factory registration).

---

## 5. Custom Registration Extensions

Create your own extension methods on `IServiceCollection` for domain-specific registration.

```csharp
public static class DomainRegistrationExtensions
{
    public static IServiceCollection AddOrderDomain(this IServiceCollection services)
    {
        services.AddSubscriber<OrderSubmitted, SendOrderConfirmation>();
        services.AddSubscriber<OrderSubmitted, UpdateInventory>();
        services.AddSubscriber<OrderSubmitted, NotifyWarehouse>();
        return services;
    }
}
```

Usage:

```csharp
services.AddLiteEventBus();
services.AddOrderDomain();
```

---

## 6. Event Bus in a Background Service

Use `IServiceScopeFactory` to publish events from background services.

```csharp
public sealed class OrderProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public OrderProcessor(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

            await eventBus.PublishAsync(new OrderSubmitted(...), stoppingToken);

            await Task.Delay(5000, stoppingToken);
        }
    }
}
```
