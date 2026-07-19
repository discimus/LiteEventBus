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
    where TEvent : IEvent
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
}
