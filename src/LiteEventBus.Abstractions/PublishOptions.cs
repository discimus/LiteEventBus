namespace LiteEventBus.Abstractions;

/// <summary>
/// Provides options for controlling the behavior of a single publish operation.
/// </summary>
public class PublishOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether publishing should continue
    /// when a subscriber throws an exception. When <see langword="true"/>,
    /// exceptions are collected and an <see cref="AggregateException"/> is
    /// thrown after all subscribers have executed. When <see langword="false"/>,
    /// the first exception propagates immediately and subsequent subscribers
    /// are not invoked.
    /// </summary>
    public bool ContinueOnError { get; set; }
}
