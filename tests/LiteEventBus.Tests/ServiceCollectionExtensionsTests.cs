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

    [Fact]
    public void AddSubscriber_DuplicateRegistration_Ignored()
    {
        var services = new ServiceCollection();
        services.AddSubscriber<TestEvent, TestSubscriber>();
        services.AddSubscriber<TestEvent, TestSubscriber>();

        var registrations = services
            .Where(sd => sd.ServiceType == typeof(IEventSubscriber<TestEvent>))
            .ToList();

        Assert.Single(registrations);
    }

    [Fact]
    public void AddSubscriber_WithDelegate_RegistersTransient()
    {
        var services = new ServiceCollection();
        services.AddSubscriber<TestEvent>((TestEvent _, CancellationToken _) => Task.CompletedTask);

        var registration = services.FirstOrDefault(
            sd => sd.ServiceType == typeof(IEventSubscriber<TestEvent>));

        Assert.NotNull(registration);
        Assert.Equal(ServiceLifetime.Transient, registration!.Lifetime);
    }

    [Fact]
    public void AddSubscriber_WithDelegate_NullServices_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((IServiceCollection)null!).AddSubscriber<TestEvent>((TestEvent _, CancellationToken _) => Task.CompletedTask));
    }

    [Fact]
    public void AddSubscriber_WithDelegate_NullHandler_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
            services.AddSubscriber<TestEvent>((Func<TestEvent, CancellationToken, Task>)null!));
    }

    [Fact]
    public void AddSubscriber_WithDelegate_ReturnsSameInstance()
    {
        var services = new ServiceCollection();
        var result = services.AddSubscriber<TestEvent>((TestEvent _, CancellationToken _) => Task.CompletedTask);
        Assert.Same(services, result);
    }

    [Fact]
    public void AddSubscriber_WithFuncTask_RegistersSuccessfully()
    {
        var services = new ServiceCollection();
        services.AddSubscriber<TestEvent>(_ => Task.CompletedTask);

        var registration = services.FirstOrDefault(
            sd => sd.ServiceType == typeof(IEventSubscriber<TestEvent>));

        Assert.NotNull(registration);
        Assert.Equal(ServiceLifetime.Transient, registration!.Lifetime);
    }

    [Fact]
    public void AddSubscriber_WithAction_RegistersSuccessfully()
    {
        var services = new ServiceCollection();
        services.AddSubscriber<TestEvent>(_ => { });

        var registration = services.FirstOrDefault(
            sd => sd.ServiceType == typeof(IEventSubscriber<TestEvent>));

        Assert.NotNull(registration);
        Assert.Equal(ServiceLifetime.Transient, registration!.Lifetime);
    }

    [Fact]
    public void AddSubscriber_DelegateMultipleCalls_EachRegisteredSeparately()
    {
        var services = new ServiceCollection();
        services.AddSubscriber<TestEvent>((TestEvent _, CancellationToken _) => Task.CompletedTask);
        services.AddSubscriber<TestEvent>((TestEvent _, CancellationToken _) => Task.CompletedTask);

        var registrations = services
            .Where(sd => sd.ServiceType == typeof(IEventSubscriber<TestEvent>))
            .ToList();

        Assert.Equal(2, registrations.Count);
    }

    [Fact]
    public void AddSubscriber_SameEventDifferentSubscribers_BothRegistered()
    {
        var services = new ServiceCollection();
        services.AddSubscriber<TestEvent, TestSubscriber>();
        services.AddSubscriber<TestEvent, ThrowingTestSubscriber>();

        var registrations = services
            .Where(sd => sd.ServiceType == typeof(IEventSubscriber<TestEvent>))
            .ToList();

        Assert.Equal(2, registrations.Count);
    }

    [Fact]
    public void AddLiteEventBus_WithConfigure_AppliesOptions()
    {
        var services = new ServiceCollection();
        services.AddLiteEventBus(options =>
        {
            options.DefaultContinueOnError = true;
        });

        var registration = services.FirstOrDefault(sd => sd.ServiceType == typeof(IEventBus));
        Assert.NotNull(registration);
    }

    [Fact]
    public void AddLiteEventBus_WithConfigureNull_DoesNotThrow()
    {
        var services = new ServiceCollection();

        var result = services.AddLiteEventBus(configure: null);

        Assert.NotNull(result);
    }

    [Fact]
    public void AddLiteEventBus_WithConfigureAndMultipleCalls_Idempotent()
    {
        var services = new ServiceCollection();
        services.AddLiteEventBus(options =>
        {
            options.DefaultContinueOnError = true;
        });
        services.AddLiteEventBus(options =>
        {
            options.DefaultContinueOnError = false;
        });

        var registrations = services.Where(sd => sd.ServiceType == typeof(IEventBus)).ToList();
        Assert.Single(registrations);
    }

    [Fact]
    public void AddSubscriber_WithServiceProviderFunc_NullServices_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((IServiceCollection)null!).AddSubscriber<TestEvent>(
                (Func<TestEvent, CancellationToken, IServiceProvider, Task>)((_, _, _) => Task.CompletedTask)));
    }

    [Fact]
    public void AddSubscriber_WithServiceProviderFunc_NullHandler_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
            services.AddSubscriber<TestEvent>(
                (Func<TestEvent, CancellationToken, IServiceProvider, Task>)null!));
    }

    [Fact]
    public void AddSubscriber_WithServiceProviderFunc_ReturnsSameInstance()
    {
        var services = new ServiceCollection();
        var result = services.AddSubscriber<TestEvent>(
            (TestEvent _, CancellationToken _, IServiceProvider _) => Task.CompletedTask);
        Assert.Same(services, result);
    }

    [Fact]
    public void AddSubscriber_WithServiceProviderFunc_RegistersTransient()
    {
        var services = new ServiceCollection();
        services.AddSubscriber<TestEvent>(
            (Func<TestEvent, CancellationToken, IServiceProvider, Task>)((_, _, _) => Task.CompletedTask));

        var registration = services.FirstOrDefault(
            sd => sd.ServiceType == typeof(IEventSubscriber<TestEvent>));

        Assert.NotNull(registration);
        Assert.Equal(ServiceLifetime.Transient, registration!.Lifetime);
    }

    [Fact]
    public void AddSubscriber_WithServiceProviderFunc_MultipleCalls_EachRegisteredSeparately()
    {
        var services = new ServiceCollection();
        services.AddSubscriber<TestEvent>(
            (Func<TestEvent, CancellationToken, IServiceProvider, Task>)((_, _, _) => Task.CompletedTask));
        services.AddSubscriber<TestEvent>(
            (Func<TestEvent, CancellationToken, IServiceProvider, Task>)((_, _, _) => Task.CompletedTask));

        var registrations = services
            .Where(sd => sd.ServiceType == typeof(IEventSubscriber<TestEvent>))
            .ToList();

        Assert.Equal(2, registrations.Count);
    }

    [Fact]
    public void AddSubscriber_WithServiceProviderAction_NullServices_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((IServiceCollection)null!).AddSubscriber<TestEvent>(
                (Action<TestEvent, CancellationToken, IServiceProvider>)((_, _, _) => { })));
    }

    [Fact]
    public void AddSubscriber_WithServiceProviderAction_NullHandler_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
            services.AddSubscriber<TestEvent>(
                (Action<TestEvent, CancellationToken, IServiceProvider>)null!));
    }

    [Fact]
    public void AddSubscriber_WithServiceProviderAction_ReturnsSameInstance()
    {
        var services = new ServiceCollection();
        var result = services.AddSubscriber<TestEvent>(
            (TestEvent _, CancellationToken _, IServiceProvider _) => { });
        Assert.Same(services, result);
    }

    [Fact]
    public void AddSubscriber_WithServiceProviderAction_RegistersTransient()
    {
        var services = new ServiceCollection();
        services.AddSubscriber<TestEvent>(
            (Action<TestEvent, CancellationToken, IServiceProvider>)((_, _, _) => { }));

        var registration = services.FirstOrDefault(
            sd => sd.ServiceType == typeof(IEventSubscriber<TestEvent>));

        Assert.NotNull(registration);
        Assert.Equal(ServiceLifetime.Transient, registration!.Lifetime);
    }

    [Fact]
    public void AddSubscriber_WithServiceProviderAction_MultipleCalls_EachRegisteredSeparately()
    {
        var services = new ServiceCollection();
        services.AddSubscriber<TestEvent>(
            (Action<TestEvent, CancellationToken, IServiceProvider>)((_, _, _) => { }));
        services.AddSubscriber<TestEvent>(
            (Action<TestEvent, CancellationToken, IServiceProvider>)((_, _, _) => { }));

        var registrations = services
            .Where(sd => sd.ServiceType == typeof(IEventSubscriber<TestEvent>))
            .ToList();

        Assert.Equal(2, registrations.Count);
    }
}
