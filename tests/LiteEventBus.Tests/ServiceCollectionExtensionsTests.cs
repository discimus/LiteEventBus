using LiteEventBus.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace LiteEventBus.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddLiteEventBus_ReturnsSameInstance()
    {
        var services = new ServiceCollection();
        var result = services.AddLiteEventBus();
        Assert.Same(services, result);
    }

    [Fact]
    public void AddLiteEventBus_MultipleCalls_NoDuplicateRegistration()
    {
        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddLiteEventBus();

        var registrations = services.Where(sd => sd.ServiceType == typeof(IEventBus)).ToList();
        Assert.Single(registrations);
    }

    [Fact]
    public void AddLiteEventBus_NullServices_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((IServiceCollection)null!).AddLiteEventBus());
    }

    [Fact]
    public void AddSubscriber_ReturnsSameInstance()
    {
        var services = new ServiceCollection();
        var result = services.AddSubscriber<TestEvent, TestSubscriber>();
        Assert.Same(services, result);
    }

    [Fact]
    public void AddSubscriber_NullServices_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((IServiceCollection)null!).AddSubscriber<TestEvent, TestSubscriber>());
    }

    [Fact]
    public void AddSubscriber_RegistersTransientLifetime()
    {
        var services = new ServiceCollection();
        services.AddSubscriber<TestEvent, TestSubscriber>();

        var registration = services.FirstOrDefault(
            sd => sd.ServiceType == typeof(IEventSubscriber<TestEvent>));

        Assert.NotNull(registration);
        Assert.Equal(ServiceLifetime.Transient, registration!.Lifetime);
        Assert.Equal(typeof(TestSubscriber), registration.ImplementationType);
    }

    [Fact]
    public void AddSubscriber_MultipleSubscribersDifferentEvents_Independent()
    {
        var services = new ServiceCollection();
        services.AddSubscriber<TestEvent, TestSubscriber>();
        services.AddSubscriber<AnotherTestEvent, AnotherEventTestSubscriber>();

        var testRegistrations = services
            .Where(sd => sd.ServiceType == typeof(IEventSubscriber<TestEvent>))
            .ToList();

        var anotherRegistrations = services
            .Where(sd => sd.ServiceType == typeof(IEventSubscriber<AnotherTestEvent>))
            .ToList();

        Assert.Single(testRegistrations);
        Assert.Single(anotherRegistrations);
    }
}
