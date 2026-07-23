using System.Linq;
using LiteEventBus;
using LiteEventBus.Abstractions;
using LiteEventBus.Internal;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
/// <summary>
/// Registers the LiteEventBus infrastructure services into the DI container.
/// </summary>
/// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
/// <returns>The same <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
public static IServiceCollection AddLiteEventBus(this IServiceCollection services)
{
    return AddLiteEventBus(services, configure: null);
}

/// <summary>
/// Registers the LiteEventBus infrastructure services into the DI container with custom options.
/// </summary>
/// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
/// <param name="configure">A delegate to configure the <see cref="EventBusOptions"/>.</param>
/// <returns>The same <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
public static IServiceCollection AddLiteEventBus(this IServiceCollection services, Action<EventBusOptions>? configure)
{
    ArgumentNullException.ThrowIfNull(services);

    if (services.Any(sd => sd.ServiceType == typeof(IEventBus)))
        return services;

    var options = new EventBusOptions();
    configure?.Invoke(options);

    services.AddSingleton<IEventBus>(sp =>
    {
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        return new DefaultEventBus(scopeFactory, options);
    });

    return services;
}

/// <summary>
/// Registers a subscriber for a specific event type.
/// </summary>
/// <typeparam name="TEvent">The type of event to subscribe to.</typeparam>
/// <typeparam name="TSubscriber">The type of subscriber that will handle the event.</typeparam>
/// <param name="services">The <see cref="IServiceCollection"/> to add the subscriber to.</param>
/// <returns>The same <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
public static IServiceCollection AddSubscriber<TEvent, TSubscriber>(this IServiceCollection services)
    where TSubscriber : class, IEventSubscriber<TEvent>
{
    ArgumentNullException.ThrowIfNull(services);

    var serviceType = typeof(IEventSubscriber<TEvent>);
    var implType = typeof(TSubscriber);

    if (services.Any(sd => sd.ServiceType == serviceType && sd.ImplementationType == implType))
        return services;

    services.AddTransient(serviceType, implType);

    return services;
}

/// <summary>
/// Registers a delegate-based subscriber for a specific event type.
/// </summary>
/// <typeparam name="TEvent">The type of event to subscribe to.</typeparam>
/// <param name="services">The <see cref="IServiceCollection"/> to add the subscriber to.</param>
/// <param name="handler">A delegate that handles the event.</param>
/// <returns>The same <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
public static IServiceCollection AddSubscriber<TEvent>(
    this IServiceCollection services,
    Func<TEvent, CancellationToken, Task> handler)
{
    ArgumentNullException.ThrowIfNull(services);
    ArgumentNullException.ThrowIfNull(handler);

    services.AddTransient<IEventSubscriber<TEvent>>(
        _ => new DelegateSubscriber<TEvent>(handler));

    return services;
}

/// <summary>
/// Registers a delegate-based subscriber for a specific event type.
/// </summary>
/// <typeparam name="TEvent">The type of event to subscribe to.</typeparam>
/// <param name="services">The <see cref="IServiceCollection"/> to add the subscriber to.</param>
/// <param name="handler">A delegate that handles the event asynchronously.</param>
/// <returns>The same <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
public static IServiceCollection AddSubscriber<TEvent>(
    this IServiceCollection services,
    Func<TEvent, Task> handler)
{
    ArgumentNullException.ThrowIfNull(services);
    ArgumentNullException.ThrowIfNull(handler);

    services.AddTransient<IEventSubscriber<TEvent>>(
        _ => new DelegateSubscriber<TEvent>((@event, _) => handler(@event)));

    return services;
}

/// <summary>
/// Registers a delegate-based subscriber for a specific event type.
/// </summary>
/// <typeparam name="TEvent">The type of event to subscribe to.</typeparam>
/// <param name="services">The <see cref="IServiceCollection"/> to add the subscriber to.</param>
/// <param name="handler">A delegate that handles the event synchronously.</param>
/// <returns>The same <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
public static IServiceCollection AddSubscriber<TEvent>(
    this IServiceCollection services,
    Action<TEvent> handler)
{
    ArgumentNullException.ThrowIfNull(services);
    ArgumentNullException.ThrowIfNull(handler);

    services.AddTransient<IEventSubscriber<TEvent>>(
        _ => new DelegateSubscriber<TEvent>((@event, _) =>
        {
            handler(@event);
            return Task.CompletedTask;
        }));

    return services;
}

/// <summary>
/// Registers a delegate-based subscriber for a specific event type with access to the
/// scoped <see cref="IServiceProvider"/> to resolve dependencies.
/// </summary>
/// <typeparam name="TEvent">The type of event to subscribe to.</typeparam>
/// <param name="services">The <see cref="IServiceCollection"/> to add the subscriber to.</param>
/// <param name="handler">A delegate that handles the event asynchronously, receiving the event, a <see cref="CancellationToken"/>, and the scoped <see cref="IServiceProvider"/>.</param>
/// <returns>The same <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
public static IServiceCollection AddSubscriber<TEvent>(
    this IServiceCollection services,
    Func<TEvent, CancellationToken, IServiceProvider, Task> handler)
{
    ArgumentNullException.ThrowIfNull(services);
    ArgumentNullException.ThrowIfNull(handler);

    services.AddTransient<IEventSubscriber<TEvent>>(
        sp => new DelegateSubscriber<TEvent>(
            (@event, ct) => handler(@event, ct, sp)));

    return services;
}

/// <summary>
/// Registers a delegate-based subscriber for a specific event type with access to the
/// scoped <see cref="IServiceProvider"/> to resolve dependencies.
/// </summary>
/// <typeparam name="TEvent">The type of event to subscribe to.</typeparam>
/// <param name="services">The <see cref="IServiceCollection"/> to add the subscriber to.</param>
/// <param name="handler">A delegate that handles the event synchronously, receiving the event, a <see cref="CancellationToken"/>, and the scoped <see cref="IServiceProvider"/>.</param>
/// <returns>The same <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
public static IServiceCollection AddSubscriber<TEvent>(
    this IServiceCollection services,
    Action<TEvent, CancellationToken, IServiceProvider> handler)
{
    ArgumentNullException.ThrowIfNull(services);
    ArgumentNullException.ThrowIfNull(handler);

    services.AddTransient<IEventSubscriber<TEvent>>(
        sp => new DelegateSubscriber<TEvent>((@event, ct) =>
        {
            handler(@event, ct, sp);
            return Task.CompletedTask;
        }));

    return services;
}
}
