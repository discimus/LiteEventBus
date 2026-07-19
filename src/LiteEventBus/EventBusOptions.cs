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
    /// the event instance, and the exception.
    /// </summary>
    public Func<IServiceProvider, IEvent, Exception, Task>? OnSubscriberError { get; set; }
}
