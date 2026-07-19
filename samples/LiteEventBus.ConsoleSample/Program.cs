using LiteEventBus.Abstractions;
using Microsoft.Extensions.DependencyInjection;

// Build the DI container
var services = new ServiceCollection();
services.AddLiteEventBus();
services.AddSubscriber<UserRegistered, SendWelcomeEmail>();
services.AddSubscriber<UserRegistered, LogAuditEntry>();
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
Console.WriteLine("Event published successfully.");

// --- Event and subscriber definitions ---

public sealed record UserRegistered(
    Guid UserId,
    string Email,
    string FullName);

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
        Console.WriteLine($"[LogAuditEntry] Audit: user {@event.UserId} ({@event.FullName}) registered");
        return Task.CompletedTask;
    }
}
