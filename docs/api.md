# API Reference

This page documents all public types and methods in the LiteEventBus library.

---

## Namespace: `LiteEventBus.Abstractions`

### `IEventBus`

The central service for publishing events. Registered as a **singleton** in the DI container.

```csharp
public interface IEventBus
{
    Task PublishAsync<TEvent>(
        TEvent @event,
        CancellationToken cancellationToken = default);

    Task PublishAsync<TEvent>(
        TEvent @event,
        PublishOptions options,
        CancellationToken cancellationToken = default);
}
```

#### Methods

| Method | Description |
|--------|-------------|
| `PublishAsync<TEvent>(TEvent, CancellationToken)` | Publishes an event using global default options. |
| `PublishAsync<TEvent>(TEvent, PublishOptions, CancellationToken)` | Publishes an event with per-call options. |

**Behavior:**

- Creates a DI scope per call.
- Resolves all `IEventSubscriber<TEvent>` registrations from the scope.
- Executes subscribers **sequentially** in registration order.
- Disposes the scope after all subscribers complete (or on first failure).

**Error handling:**

- Default: first exception propagates immediately; remaining subscribers skipped.
- With `ContinueOnError`: all subscribers execute; exceptions collected into `AggregateException`.

**Exceptions:**

| Exception | Condition |
|-----------|-----------|
| `ArgumentNullException` | `@event` is `null` |
| `InvalidOperationException` | Subscriber throws (fail-fast mode) |
| `AggregateException` | One or more subscribers threw and `ContinueOnError` was `true` |
| `OperationCanceledException` | `CancellationToken` was cancelled |

---

### `IEventSubscriber<TEvent>`

Contract for handling events of a specific type.

```csharp
public interface IEventSubscriber<in TEvent>
{
    Task HandleAsync(
        TEvent @event,
        CancellationToken cancellationToken);
}
```

#### Type Parameters

| Parameter | Constraint | Description |
|-----------|------------|-------------|
| `TEvent` | Contravariant (`in`) | The type of event this subscriber handles. |

#### Methods

| Method | Description |
|--------|-------------|
| `HandleAsync(TEvent, CancellationToken)` | Processes the event. Called by the event bus for each published event. |

**Implementation notes:**

- Keep handlers short and async where possible.
- Inject dependencies via constructor.
- The `cancellationToken` should be passed to any I/O operations within the handler.

---

### `PublishOptions`

Controls the behavior of a single publish operation.

```csharp
public class PublishOptions
{
    public bool ContinueOnError { get; set; }
}
```

#### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ContinueOnError` | `bool` | `false` | When `true`, all subscribers execute even if some throw. Exceptions are collected into an `AggregateException`. When `false`, the first exception propagates immediately and remaining subscribers are not invoked. |

When no `PublishOptions` is passed to `PublishAsync`, the value of `EventBusOptions.DefaultContinueOnError` is used as the fallback.

---

## Namespace: `LiteEventBus`

### `EventBusOptions`

Global configuration for the LiteEventBus infrastructure.

```csharp
public sealed class EventBusOptions
{
    public bool DefaultContinueOnError { get; set; }
    public Func<IServiceProvider, object, Exception, Task>? OnSubscriberError { get; set; }
}
```

#### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DefaultContinueOnError` | `bool` | `false` | Default value for `ContinueOnError` when no `PublishOptions` is provided to `PublishAsync`. |
| `OnSubscriberError` | `Func<IServiceProvider, object, Exception, Task>?` | `null` | Callback invoked per subscriber failure when `ContinueOnError` is `true`. Receives the scope-level `IServiceProvider`, the event instance, and the exception. Callback exceptions are swallowed. |

---

## Namespace: `Microsoft.Extensions.DependencyInjection`

### `ServiceCollectionExtensions`

Extension methods on `IServiceCollection` for registering LiteEventBus infrastructure and subscribers.

#### `AddLiteEventBus`

Registers `IEventBus` as a singleton.

```csharp
public static IServiceCollection AddLiteEventBus(
    this IServiceCollection services);

public static IServiceCollection AddLiteEventBus(
    this IServiceCollection services,
    Action<EventBusOptions>? configure);
```

**Idempotent:** If `IEventBus` is already registered, subsequent calls are no-ops. The `configure` delegate from the first call is used; later delegates are ignored.

**Exceptions:**

| Exception | Condition |
|-----------|-----------|
| `ArgumentNullException` | `services` is `null` |

---

#### `AddSubscriber<TEvent, TSubscriber>`

Registers a class-based subscriber as **transient**.

```csharp
public static IServiceCollection AddSubscriber<TEvent, TSubscriber>(
    this IServiceCollection services)
    where TSubscriber : class, IEventSubscriber<TEvent>;
```

**Deduplication:** If the same pair `(TEvent, TSubscriber)` is already registered, the call is silently ignored.

**Exceptions:**

| Exception | Condition |
|-----------|-----------|
| `ArgumentNullException` | `services` is `null` |

---

#### `AddSubscriber<TEvent>` (delegate overloads)

Registers a lambda-based subscriber as **transient**. Three overloads accept different delegate signatures:

```csharp
// Full async with cancellation token
public static IServiceCollection AddSubscriber<TEvent>(
    this IServiceCollection services,
    Func<TEvent, CancellationToken, Task> handler);

// Async without cancellation token
public static IServiceCollection AddSubscriber<TEvent>(
    this IServiceCollection services,
    Func<TEvent, Task> handler);

// Synchronous
public static IServiceCollection AddSubscriber<TEvent>(
    this IServiceCollection services,
    Action<TEvent> handler);
```

**No deduplication:** Each call registers a new subscriber. The same delegate registered twice will be invoked twice per publish.

**Exceptions:**

| Exception | Condition |
|-----------|-----------|
| `ArgumentNullException` | `services` or `handler` is `null` |
