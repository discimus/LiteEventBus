# Public Types Reference

## Interfaces

### `IEventBus`

**Namespace:** `LiteEventBus.Abstractions`

The central service for publishing events. Registered as singleton.

```csharp
public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default);
    Task PublishAsync<TEvent>(TEvent @event, PublishOptions options, CancellationToken ct = default);
}
```

---

### `IEventSubscriber<TEvent>`

**Namespace:** `LiteEventBus.Abstractions`

Contract for handling events of a specific type. Generic parameter is contravariant (`in`).

```csharp
public interface IEventSubscriber<in TEvent>
{
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken);
}
```

---

## Classes

### `PublishOptions`

**Namespace:** `LiteEventBus.Abstractions`

Per-call options for controlling a single publish operation.

```csharp
public class PublishOptions
{
    public bool ContinueOnError { get; set; }
}
```

---

### `EventBusOptions`

**Namespace:** `LiteEventBus`

Global configuration for the LiteEventBus infrastructure. Set during `AddLiteEventBus(...)`.

```csharp
public sealed class EventBusOptions
{
    public bool DefaultContinueOnError { get; set; }
    public Func<IServiceProvider, object, Exception, Task>? OnSubscriberError { get; set; }
}
```

---

## Static Classes

### `ServiceCollectionExtensions`

**Namespace:** `Microsoft.Extensions.DependencyInjection`

Extension methods on `IServiceCollection` for registering LiteEventBus services.

```csharp
public static class ServiceCollectionExtensions
{
    // Registration
    public static IServiceCollection AddLiteEventBus(this IServiceCollection services);
    public static IServiceCollection AddLiteEventBus(this IServiceCollection services, Action<EventBusOptions>? configure);

    // Class-based subscriber
    public static IServiceCollection AddSubscriber<TEvent, TSubscriber>(this IServiceCollection services)
        where TSubscriber : class, IEventSubscriber<TEvent>;

    // Delegate subscribers
    public static IServiceCollection AddSubscriber<TEvent>(this IServiceCollection services, Func<TEvent, CancellationToken, Task> handler);
    public static IServiceCollection AddSubscriber<TEvent>(this IServiceCollection services, Func<TEvent, Task> handler);
    public static IServiceCollection AddSubscriber<TEvent>(this IServiceCollection services, Action<TEvent> handler);
}
```

---

## Internal Types (Not for Public Consumption)

These types are `internal` and subject to change:

| Type | Location | Purpose |
|------|----------|---------|
| `DefaultEventBus` | `LiteEventBus.Internal` | `IEventBus` implementation |
| `DelegateSubscriber<TEvent>` | `LiteEventBus.Internal` | Wraps delegates as `IEventSubscriber<TEvent>` |
