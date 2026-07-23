# Getting Started

This guide walks through setting up LiteEventBus from scratch in a console application.

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- A terminal or command prompt

## 1. Create a new console project

```shell
dotnet new console -n LiteEventBusDemo
cd LiteEventBusDemo
```

## 2. Add the package

```shell
dotnet add package LiteEventBus
```

## 3. Define an event

An event is a plain object that carries information about something that happened.

```csharp
// Events.cs
public sealed record UserRegistered(
    Guid UserId,
    string Email,
    string FullName);
```

## 4. Create subscribers

Subscribers implement `IEventSubscriber<TEvent>` to react to events.

```csharp
// Handlers.cs
using LiteEventBus.Abstractions;

public sealed class SendWelcomeEmail : IEventSubscriber<UserRegistered>
{
    public Task HandleAsync(UserRegistered @event, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[SendWelcomeEmail] Welcome email sent to {@event.Email}");
        return Task.CompletedTask;
    }
}

public sealed class LogAuditEntry : IEventSubscriber<UserRegistered>
{
    public Task HandleAsync(UserRegistered @event, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[LogAuditEntry] User {@event.UserId} ({@event.FullName}) registered");
        return Task.CompletedTask;
    }
}
```

## 5. Wire everything up

```csharp
// Program.cs
using LiteEventBus.Abstractions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Register the event bus infrastructure
services.AddLiteEventBus();

// Register subscribers
services.AddSubscriber<UserRegistered, SendWelcomeEmail>();
services.AddSubscriber<UserRegistered, LogAuditEntry>();

// (Optional) Lambda subscriber — no dedicated class needed
services.AddSubscriber<UserRegistered>(@event =>
    Console.WriteLine($"[Lambda] User name: {@event.FullName}"));

// (Optional) Lambda with IServiceProvider — resolve services on the fly
services.AddSubscriber<UserRegistered>((e, ct, sp) =>
{
    var emailService = sp.GetRequiredService<SendWelcomeEmail>();
    return emailService.HandleAsync(e, ct);
});

var provider = services.BuildServiceProvider();

// Resolve the event bus
var eventBus = provider.GetRequiredService<IEventBus>();

// Publish an event
var userRegistered = new UserRegistered(
    Guid.NewGuid(),
    "john.doe@example.com",
    "John Doe");

Console.WriteLine("Publishing UserRegistered event...");
await eventBus.PublishAsync(userRegistered);
Console.WriteLine("Done.");
```

## 6. Run the application

```shell
dotnet run
```

### Expected output

```
Publishing UserRegistered event...
[SendWelcomeEmail] Welcome email sent to john.doe@example.com
[LogAuditEntry] User 550e8400-e29b-41d4-a716-446655440000 (John Doe) registered
[Lambda] User name: John Doe
Done.
```

Subscribers execute **sequentially** in the order they were registered. Each subscriber receives the same event instance.

## Next Steps

- Learn about [configuration options](configuration.md) for error handling.
- Understand the [architecture](architecture.md) and how scopes work.
- See [advanced examples](examples/advanced.md) including scoped dependencies and EF Core.
