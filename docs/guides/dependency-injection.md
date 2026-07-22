# Dependency Injection

## Overview

LiteEventBus is built around `Microsoft.Extensions.DependencyInjection` and follows its lifetime conventions.

---

## Service Lifetimes

| Service | Lifetime | Reason |
|---------|----------|--------|
| `IEventBus` | **Singleton** | Stateless dispatcher. Safe and efficient shared across the application. |
| `IEventSubscriber<TEvent>` | **Transient** | New instance per-publish. Avoids state leaking between publishes. |

Subscribers are always registered as **transient** regardless of whether you use the class-based or delegate-based `AddSubscriber` overload.

---

## DI Container Lifecycle for Publishing

```mermaid
sequenceDiagram
    participant App
    participant Root as Root Container
    participant Bus as IEventBus (Singleton)
    participant SF as IServiceScopeFactory
    participant Scope
    participant Sub as IEventSubscriber&lt;T&gt;

    App->>Bus: PublishAsync(event)
    Bus->>SF: CreateScope()
    Bus->>Scope: Resolve subscribers
    Scope->>Sub: new instance
    Bus->>Sub: HandleAsync(event)
    Sub->>Scope: (disposed after)
    Bus-->>App: completed
```

Each `PublishAsync` creates a scope. Subscribers and their dependencies are resolved from that scope. After publishing completes, the scope is disposed.

---

## Scoped Dependencies in Subscribers

Because each publish creates a new scope, subscribers can safely inject scoped services.

```csharp
public sealed class OrderHandler : IEventSubscriber<OrderSubmitted>
{
    private readonly AppDbContext _db;

    public OrderHandler(AppDbContext db)
    {
        _db = db; // Resolved from the publish scope
    }

    public async Task HandleAsync(OrderSubmitted @event, CancellationToken ct)
    {
        _db.Orders.Add(new Order { Id = @event.OrderId });
        await _db.SaveChangesAsync(ct);
    }
}
```

Registration:

```csharp
services.AddDbContext<AppDbContext>(options => ...);
services.AddLiteEventBus();
services.AddSubscriber<OrderSubmitted, OrderHandler>();
```

> **Important:** Do not register `IEventSubscriber<T>` as scoped or singleton. Transient is required for correct scope isolation.

---

## Console Application

```csharp
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddLiteEventBus();
services.AddSubscriber<OrderSubmitted, OrderHandler>();

var provider = services.BuildServiceProvider();
var eventBus = provider.GetRequiredService<IEventBus>();

await eventBus.PublishAsync(new OrderSubmitted(...));
```

---

## ASP.NET Core

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLiteEventBus(options =>
{
    options.DefaultContinueOnError = true;
});

builder.Services.AddSubscriber<OrderSubmitted, OrderHandler>();

var app = builder.Build();
```

Inject `IEventBus` into controllers, services, or middlewares:

```csharp
[ApiController]
public class OrdersController : ControllerBase
{
    private readonly IEventBus _eventBus;

    public OrdersController(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    [HttpPost]
    public async Task<IActionResult> Create(OrderRequest request)
    {
        // ... create order ...
        await _eventBus.PublishAsync(new OrderSubmitted(orderId, request.Email, request.Total));
        return Ok();
    }
}
```

---

## Idempotent Registration

Both `AddLiteEventBus` and `AddSubscriber<TEvent, TSubscriber>` are idempotent:

```csharp
// Safe to call multiple times
services.AddLiteEventBus();
services.AddLiteEventBus(); // No-op
services.AddLiteEventBus(); // No-op

// Only one IEventBus registration exists
```

Class-based subscribers are also deduplicated:

```csharp
services.AddSubscriber<OrderSubmitted, OrderHandler>();
services.AddSubscriber<OrderSubmitted, OrderHandler>(); // No-op
```

Lambda delegates are **not** deduplicated — each call registers a separate subscriber:

```csharp
services.AddSubscriber<OrderSubmitted>(@event => Console.WriteLine("A"));
services.AddSubscriber<OrderSubmitted>(@event => Console.WriteLine("A"));
// Both will run on publish
```

---

## Best Practices

1. **Register `IEventBus` once.** `AddLiteEventBus` is idempotent.
2. **Register subscribers as transient** (the default when using the provided extension methods).
3. **Do not manually register `IEventBus`** with a different lifetime.
4. **Do not capture scoped services outside of a subscriber** — always resolve via constructor injection.
5. **Keep subscriber constructors simple.** Any expensive initialization belongs in `HandleAsync`.
