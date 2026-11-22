using LiteEventBus.Abstractions;

namespace LiteEventBus;

/// <summary>
/// Provides options for configuring the LiteEventBus infrastructure during registration.
/// </summary>
public sealed class EventBusOptions
{
    /// <summary>
    /// Gets or sets the default value for <see cref="PublishOptions.ContinueOnError"/>
    /// when no per-call options are provided. The default is <see langword="false"/>.
    /// </summary>
    public bool DefaultContinueOnError { get; set; }

    /// <summary>
    /// Gets or sets a callback that is invoked when a subscriber throws an exception
    /// and <see cref="DefaultContinueOnError"/> is <see langword="true"/>.
    /// The callback receives the scope-level <see cref="IServiceProvider"/>,
    /// the event instance (typed as <see cref="object"/> — use <c>@event.GetType()</c> to retrieve
    /// the original event type), and the exception.
    /// </summary>
    /// <remarks>
    /// The <see cref="IServiceProvider"/> provided to the callback is the DI scope created for the
    /// current <c>PublishAsync</c> call. Services resolved from it share the same scope lifetime as
    /// the publish operation. Do not store the provider reference beyond the callback's execution
    /// to avoid scope leaks.
    /// Exceptions thrown by this callback are silently swallowed to avoid interrupting the publish flow.
    /// </remarks>
    public Func<IServiceProvider, object, Exception, Task>? OnSubscriberError { get; set; }
}
