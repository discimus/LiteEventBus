using LiteEventBus.Abstractions;

namespace LiteEventBus.Tests;

internal sealed record TestEvent : IEvent;

internal sealed record AnotherTestEvent : IEvent;
