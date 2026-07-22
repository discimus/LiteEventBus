# Options Reference

## `EventBusOptions`

Configured via `AddLiteEventBus(Action<EventBusOptions>)`.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DefaultContinueOnError` | `bool` | `false` | Default error behavior when no per-call `PublishOptions` is provided. `false` = fail-fast on first exception; `true` = execute all subscribers and aggregate exceptions. |
| `OnSubscriberError` | `Func<IServiceProvider, object, Exception, Task>?` | `null` | Callback invoked per subscriber failure when `ContinueOnError` is `true`. Receives scope provider, event instance, and exception. Returned task is awaited before continuing to the next subscriber. If the callback throws, the exception is swallowed. |

---

## `PublishOptions`

Passed to `IEventBus.PublishAsync(TEvent, PublishOptions, CancellationToken)`.

| Property | Type | Default | Overrides | Description |
|----------|------|---------|-----------|-------------|
| `ContinueOnError` | `bool` | (falls back to `EventBusOptions.DefaultContinueOnError`) | ✅ Per-call | When `true`, all subscribers execute even on error. Exceptions collected into `AggregateException`. Always `false` when `null` options are passed and no global default is set. |

---

## Resolution Priority

1. **Per-call** `PublishOptions.ContinueOnError` — highest priority.
2. **Global** `EventBusOptions.DefaultContinueOnError` — used when no per-call options provided.
3. **Library default** — `false` (fail-fast) when neither is configured.

---

## Behavior Matrix

| DefaultContinueOnError | PublishOptions provided | PublishOptions.ContinueOnError | Effective behavior |
|------------------------|------------------------|-------------------------------|--------------------|
| `false` (default) | No | — | Fail-fast |
| `false` (default) | Yes | `false` | Fail-fast |
| `false` (default) | Yes | `true` | Continue-on-error |
| `true` | No | — | Continue-on-error |
| `true` | Yes | `false` | Fail-fast (overrides global) |
| `true` | Yes | `true` | Continue-on-error |
