# Conventions

## Naming Conventions

| Convention | Rule |
|------------|------|
| Events | Past-tense name describing something that happened (e.g., `OrderSubmitted`, `UserRegistered`). Immutable records preferred. |
| Subscribers | Describe the action taken (e.g., `SendWelcomeEmail`, `UpdateInventory`, `LogAuditEntry`). |
| Event types | PascalCase suffix matching domain concept. |
| Subscriber types | PascalCase with verb prefix. |
| Event bus | Always resolved via `IEventBus` interface. |

## Design Principles

The library follows these principles in priority order:

### KISS (Keep It Simple, Stupid)

The simplest possible implementation that solves the problem. No unnecessary abstraction layers, no configuration-driven behavior, no conventions scanning.

### YAGNI (You Ain't Gonna Need It)

Only features that are currently needed are implemented. LiteEventBus does not include:

- Distributed messaging (RabbitMQ, Kafka, etc.)
- Mediator or CQRS
- Pipeline behaviors or middleware
- Retry, dead-letter, or persistence
- Reflection scanning or source generators
- Synchronous publish API

### Zero Configuration

The library works with a single line: `services.AddLiteEventBus()`. Configuration is purely additive and only needed for custom error handling.

### Thread Safety

The `DefaultEventBus` holds no mutable shared state. All `PublishAsync` calls are independent. Concurrent calls are safe.

### Performance

- No reflection, `dynamic`, or `Activator` in the publish path.
- Subscriber resolution uses DI's built-in `IEnumerable<T>` support.
- `ConfigureAwait(false)` throughout internal code.
- No allocations beyond scope creation and subscriber resolution.

---

## Coding Standards (Library Internals)

| Rule | Standard |
|------|----------|
| Language | C# 12 |
| Framework | .NET 8 |
| Nullability | Enabled (`Nullable: enable`) |
| Namespaces | File-scoped |
| Visibility | `sealed` when possible, `readonly` when applicable |
| Parameter validation | `ArgumentNullException.ThrowIfNull` |
| Async pattern | `ConfigureAwait(false)` in library code |
| Documentation | XML doc on all public API |
| Build warnings | Treated as errors |
| Analyzers | .NET analyzers enabled, code style enforced in build |

---

## Event Naming

Events should describe something that **has already happened** in the past tense.

```csharp
// Good
public sealed record OrderSubmitted(...);
public sealed record UserRegistered(...);
public sealed record PaymentProcessed(...);

// Avoid — sounds like a command
public sealed record CreateOrder(...);
public sealed record SendEmail(...);
```

---

## Subscriber Naming

Subscribers should describe the action they perform, prefixed with a verb.

```csharp
// Good
public sealed class SendWelcomeEmail : IEventSubscriber<UserRegistered> { }
public sealed class UpdateInventory : IEventSubscriber<OrderSubmitted> { }

// Ambiguous
public sealed class EmailHandler : IEventSubscriber<UserRegistered> { }
public sealed class Inventory : IEventSubscriber<OrderSubmitted> { }
```

---

## DI Registration

- Always use `AddSubscriber<TEvent, TSubscriber>` or `AddSubscriber<TEvent>(...)` extension methods.
- Never register `IEventBus` manually.
- Never register `IEventSubscriber<T>` as scoped or singleton.
