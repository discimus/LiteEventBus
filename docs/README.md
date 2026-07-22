# LiteEventBus Documentation

LiteEventBus is a lightweight .NET in-memory publish/subscribe library with zero external dependencies beyond `Microsoft.Extensions.DependencyInjection.Abstractions`.

- **Publish/subscribe** — strongly-typed events, in-memory delivery.
- **DI-first** — built for `Microsoft.Extensions.DependencyInjection`.
- **Zero configuration** — one line to register, then publish.
- **Sequential execution** — subscribers run in registration order.
- **Error resilience** — fail-fast or continue-on-error with error callbacks.

---

## Learning Path

New to LiteEventBus? Follow this progression:

1. **[Installation](installation.md)** — add the NuGet package.
2. **[Getting Started](getting-started.md)** — build your first pub/sub flow in 5 minutes.
3. **[Core Concepts](concepts.md)** — understand events, subscribers, scopes, and error modes.
4. **[Configuration](configuration.md)** — configure error handling and callbacks.
5. **[Basic Examples](examples/basic.md)** — common usage patterns.
6. **[Advanced Examples](examples/advanced.md)** — scoped dependencies, concurrency, EF Core.
7. **[Architecture](architecture.md)** — how it works under the hood.
8. **[Real-World Examples](examples/real-world.md)** — ASP.NET Core, background services.

---

## Documentation Map

### Getting Started

| Page | Description |
|------|-------------|
| [Installation](installation.md) | NuGet package, dependencies, versioning |
| [Getting Started](getting-started.md) | Quick start with a complete working example |

### Fundamentals

| Page | Description |
|------|-------------|
| [Core Concepts](concepts.md) | Events, subscribers, event bus, scopes, error modes |
| [Architecture](architecture.md) | System design, module responsibilities, lifecycle, Mermaid diagrams |
| [Configuration](configuration.md) | All configuration options with examples |

### API Reference

| Page | Description |
|------|-------------|
| [API Reference](api.md) | Complete documentation of all public types and methods |
| [Public Types](reference/public-types.md) | Quick reference of all public interfaces and classes |
| [Options Reference](reference/options.md) | Consolidated configuration option reference |
| [Conventions](reference/conventions.md) | Naming conventions, design principles, coding standards |

### Examples

| Page | Description |
|------|-------------|
| [Basic Examples](examples/basic.md) | Events, subscribers, lambdas, error handling |
| [Advanced Examples](examples/advanced.md) | Scoped dependencies, concurrency, transient behavior |
| [Real-World Examples](examples/real-world.md) | ASP.NET Core, background services, structured logging |

### Guides

| Page | Description |
|------|-------------|
| [Dependency Injection](guides/dependency-injection.md) | DI lifetimes, console apps, ASP.NET Core integration |
| [Testing](guides/testing.md) | Unit testing subscribers, integration testing with IEventBus |
| [Extensibility](guides/extensibility.md) | Custom subscribers, decorators, error callbacks, background services |
| [Troubleshooting](guides/troubleshooting.md) | Common issues and solutions |

---

## Quick Links

- **NuGet:** `dotnet add package LiteEventBus`
- **Source:** [github.com/discimus/LiteEventBus](https://github.com/discimus/LiteEventBus)
- **License:** MIT

### Minimal Example

```csharp
using LiteEventBus.Abstractions;
using Microsoft.Extensions.DependencyInjection;

// Define event
public sealed record OrderSubmitted(Guid OrderId);

// Define subscriber
public sealed class OrderHandler : IEventSubscriber<OrderSubmitted>
{
    public Task HandleAsync(OrderSubmitted @event, CancellationToken ct)
    {
        Console.WriteLine($"Order {@event.OrderId} received");
        return Task.CompletedTask;
    }
}

// Wire up
var services = new ServiceCollection();
services.AddLiteEventBus();
services.AddSubscriber<OrderSubmitted, OrderHandler>();
var provider = services.BuildServiceProvider();

// Publish
var eventBus = provider.GetRequiredService<IEventBus>();
await eventBus.PublishAsync(new OrderSubmitted(Guid.NewGuid()));
```

### Limitations

- In-memory only — no distributed messaging.
- No Mediator, CQRS, or Command Bus patterns.
- No pipeline behaviors, middleware, or filters.
- No retry, dead-letter, or persistence.
- Async-only API.
- No reflection scanning or source generators.
