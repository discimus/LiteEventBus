# Configuration

LiteEventBus offers two layers of configuration: **global options** set at registration time and **per-call options** set at publish time.

---

## Global Options (`EventBusOptions`)

Configured during `AddLiteEventBus(...)`:

```csharp
services.AddLiteEventBus(options =>
{
    options.DefaultContinueOnError = true;
    options.OnSubscriberError = async (sp, @event, exception) =>
    {
        var logger = sp.GetRequiredService<ILogger<IEventBus>>();
        logger.LogError(exception, "Subscriber failed while processing {EventType}",
            @event.GetType().Name);
    };
});
```

### `DefaultContinueOnError`

| Type | Default | Description |
|------|---------|-------------|
| `bool` | `false` | Default value for `PublishOptions.ContinueOnError` when no per-call options are provided. |

When `false`, the first subscriber exception stops execution immediately. When `true`, all subscribers execute and exceptions are aggregated into an `AggregateException`.

### `OnSubscriberError`

| Type | Default | Description |
|------|---------|-------------|
| `Func<IServiceProvider, object, Exception, Task>?` | `null` | Callback invoked when a subscriber throws an exception **and** `ContinueOnError` is `true`. |

The callback receives:

- `IServiceProvider` — the scope-level provider (can resolve scoped services).
- `object` — the event instance that caused the error.
- `Exception` — the exception thrown by the subscriber.

If the callback itself throws, the exception is **swallowed** to avoid breaking the publish flow.

---

## Per-Call Options (`PublishOptions`)

Passed directly to `PublishAsync`:

```csharp
var options = new PublishOptions { ContinueOnError = true };

await eventBus.PublishAsync(new OrderSubmitted(...), options);
```

### `ContinueOnError`

| Type | Default (when null) | Description |
|------|---------------------|-------------|
| `bool` | `EventBusOptions.DefaultContinueOnError` | When `true`, all subscribers execute even on error. Exceptions are collected and thrown as `AggregateException`. |

Per-call options always override the global default. When no `PublishOptions` are provided, the value of `EventBusOptions.DefaultContinueOnError` is used.

---

## Configuration Examples

### Minimal

```csharp
services.AddLiteEventBus();
```

All defaults apply. Subscribers are fail-fast. No error callback.

### Recommended (production)

```csharp
services.AddLiteEventBus(options =>
{
    options.DefaultContinueOnError = true;
    options.OnSubscriberError = async (sp, @event, exception) =>
    {
        var logger = sp.GetRequiredService<ILogger<IEventBus>>();
        logger.LogError(exception,
            "Subscriber {SubscriberType} failed for {EventType}",
            exception.TargetSite?.DeclaringType?.Name ?? "unknown",
            @event.GetType().Name);
    };
});
```

Continue-on-error ensures all subscribers execute. The error callback provides visibility without breaking the flow.

### Fail-fast with logging (per-event override)

```csharp
services.AddLiteEventBus(options =>
{
    options.OnSubscriberError = (sp, @event, ex) =>
    {
        Console.WriteLine($"ERROR: {ex.Message}");
        return Task.CompletedTask;
    };
});
```

Global default is fail-fast. `OnSubscriberError` is only invoked when a call explicitly uses `ContinueOnError = true`.

---

## Resolution Order

1. If `PublishOptions` is provided to `PublishAsync`, its `ContinueOnError` is used.
2. Otherwise, `EventBusOptions.DefaultContinueOnError` is used.
3. If neither is set, the default is `false` (fail-fast).
