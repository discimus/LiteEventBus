# Troubleshooting

## Subscriber Is Not Called

**Possible causes:**

1. **Wrong event type registration.** Ensure the subscriber is registered for the exact event type you are publishing.

   ```csharp
   // Registration
   services.AddSubscriber<OrderSubmitted, OrderHandler>();

   // Publishing — must be same type
   await eventBus.PublishAsync(new OrderSubmitted(...));
   ```

2. **DI container not built or bus not resolved.** Verify you are resolving `IEventBus` from a built provider.

   ```csharp
   var provider = services.BuildServiceProvider();
   var eventBus = provider.GetRequiredService<IEventBus>();
   ```

3. **Registration order confusion.** `AddSubscriber` must be called **after** `AddLiteEventBus` (though DI registration order does not matter, subscribers must be registered before you expect them to run).

4. **Duplicate registration was ignored.** The class-based `AddSubscriber<TEvent, TSubscriber>` silently ignores duplicates. If you expected two instances of the same subscriber type to run, use different types or delegate registration.

5. **Exception swallowed by ContinueOnError without visible error.** If `ContinueOnError` is `true` but there is no `OnSubscriberError` callback, subscriber exceptions are collected and an `AggregateException` is thrown at the end. Ensure you handle or observe the exception.

---

## `AggregateException` Thrown Unexpectedly

**Cause:** One or more subscribers threw exceptions and `ContinueOnError` was `true` (either via global default or per-call options).

**Solution:**

- Check if `DefaultContinueOnError` or per-call `PublishOptions` is unintentionally set to `true`.
- Add an `OnSubscriberError` callback to log the failures.
- Wrap publish calls in try/catch for `AggregateException`.

```csharp
try
{
    await eventBus.PublishAsync(new OrderSubmitted(...));
}
catch (AggregateException ex)
{
    foreach (var inner in ex.InnerExceptions)
    {
        Console.WriteLine($"Subscriber error: {inner.Message}");
    }
}
```

---

## Scoped Dependency Throws "Cannot resolve scoped service from root provider"

**Cause:** `IEventSubscriber<TEvent>` was registered as singleton or scoped instead of transient.

**Solution:** Always use the `AddSubscriber` extension methods, which register subscribers as transient. If manually registering, use `AddTransient`:

```csharp
// Correct
services.AddTransient<IEventSubscriber<OrderSubmitted>, OrderHandler>();

// Wrong — will cause errors with scoped dependencies
services.AddSingleton<IEventSubscriber<OrderSubmitted>, OrderHandler>();
```

---

## Operation Canceled Exception

**Cause:** The `CancellationToken` passed to `PublishAsync` was cancelled.

**Possible scenarios:**

- Application is shutting down.
- Request was aborted (ASP.NET Core).
- Timeout was exceeded.
- A subscriber explicitly cancelled the token.

`OperationCanceledException` always propagates, even when `ContinueOnError` is `true`. This is by design to respect cancellation.

---

## Subscriber Runs Multiple Times Per Publish

**Possible causes:**

1. **Lambda subscriber registered multiple times.** Delegate-based `AddSubscriber<TEvent>` does not deduplicate — each call registers a separate subscriber.

   ```csharp
   // Both run on every publish
   services.AddSubscriber<OrderSubmitted>(@event => Console.WriteLine("A"));
   services.AddSubscriber<OrderSubmitted>(@event => Console.WriteLine("A"));
   ```

2. **Multiple `AddLiteEventBus` calls with different configurations.** While `AddLiteEventBus` is idempotent, only the first call's configuration is used. The second call is a no-op.

---

## Event Published but No Subscribers Found

This is a valid scenario. Publishing an event type with **zero** registered subscribers completes without error. No exception is thrown.

If you expected subscribers to exist, verify that `AddSubscriber` was called for the exact event type.

---

## Memory or Resource Leaks

**Cause:** Holding references to heavy objects in subscriber instances.

**Solution:**

- Subscribers are transient — a new instance is created per publish. If you inject singleton services, ensure they do not accumulate state per subscriber.
- Avoid capturing large objects in lambda closures.

---

## Concurrent Publishing Issues

Publishing the same event type concurrently is safe. However:

- Subscribers share no ordering guarantees across concurrent publishes.
- If your subscriber uses a shared resource (e.g., a file), you must provide your own synchronization.

---

## "AddLiteEventBus" Configuration Ignored

**Cause:** `AddLiteEventBus` was called multiple times with different `configure` delegates. Only the **first** call's delegate is applied. Subsequent calls are no-ops.

```csharp
// The second call is ignored entirely
services.AddLiteEventBus(options =>
{
    options.DefaultContinueOnError = true; // This is used
});

services.AddLiteEventBus(options =>
{
    options.DefaultContinueOnError = false; // This is ignored
});
```
