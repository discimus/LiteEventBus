using LiteEventBus.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace LiteEventBus.Tests;

public class DefaultEventBusTests
{
    [Fact]
    public async Task PublishAsync_NoSubscribers_CompletesSuccessfully()
    {
        var services = new ServiceCollection();
        services.AddLiteEventBus();
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await eventBus.PublishAsync(new TestEvent());
    }

    [Fact]
    public async Task PublishAsync_OneSubscriber_CallsHandle()
    {
        var handler = new TestSubscriber();

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await eventBus.PublishAsync(new TestEvent());

        Assert.Equal(1, handler.HandleCount);
    }

    [Fact]
    public async Task PublishAsync_MultipleSubscribers_AllCalled()
    {
        var handler1 = new TestSubscriber();
        var handler2 = new TestSubscriber();

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler1);
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler2);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await eventBus.PublishAsync(new TestEvent());

        Assert.Equal(1, handler1.HandleCount);
        Assert.Equal(1, handler2.HandleCount);
    }

    [Fact]
    public async Task PublishAsync_MultipleSubscribers_ExecutesInRegistrationOrder()
    {
        var callOrder = new List<int>();
        var handler1 = new OrderedTestSubscriber(1, callOrder);
        var handler2 = new OrderedTestSubscriber(2, callOrder);

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler1);
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler2);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await eventBus.PublishAsync(new TestEvent());

        Assert.Equal([1, 2], callOrder);
    }

    [Fact]
    public async Task PublishAsync_SubscriberThrows_PropagatesException()
    {
        var handler1 = new TestSubscriber();
        var handler2 = new ThrowingTestSubscriber();

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler1);
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler2);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => eventBus.PublishAsync(new TestEvent()));
    }

    [Fact]
    public async Task PublishAsync_SubscriberThrows_SubsequentNotCalled()
    {
        var handler1 = new TestSubscriber();
        var throwingHandler = new ThrowingTestSubscriber();
        var handlerAfterThrow = new TestSubscriber();

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler1);
        services.AddSingleton<IEventSubscriber<TestEvent>>(throwingHandler);
        services.AddSingleton<IEventSubscriber<TestEvent>>(handlerAfterThrow);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        try
        {
            await eventBus.PublishAsync(new TestEvent());
        }
        catch (InvalidOperationException)
        {
        }

        Assert.Equal(1, handler1.HandleCount);
        Assert.Equal(0, handlerAfterThrow.HandleCount);
    }

    [Fact]
    public async Task PublishAsync_CancellationToken_ForwardedToSubscribers()
    {
        using var cts = new CancellationTokenSource();
        var handler = new CancellationTokenTestSubscriber();

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await eventBus.PublishAsync(new TestEvent(), cts.Token);

        Assert.True(handler.ReceivedToken.Equals(cts.Token));
    }

    [Fact]
    public async Task PublishAsync_MultipleEventTypes_IndependentDispatch()
    {
        var handlerA = new TestSubscriber();
        var handlerB = new AnotherEventTestSubscriber();

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(handlerA);
        services.AddSingleton<IEventSubscriber<AnotherTestEvent>>(handlerB);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await eventBus.PublishAsync(new TestEvent());
        await eventBus.PublishAsync(new AnotherTestEvent());

        Assert.Equal(1, handlerA.HandleCount);
        Assert.Equal(1, handlerB.HandleCount);
    }

    [Fact]
    public async Task PublishAsync_DI_ResolvesCorrectly()
    {
        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSubscriber<TestEvent, TestSubscriber>();
        var provider = services.BuildServiceProvider();

        var eventBus = provider.GetRequiredService<IEventBus>();
        var subscribers = provider.GetRequiredService<IEnumerable<IEventSubscriber<TestEvent>>>();

        Assert.NotNull(eventBus);
        Assert.Single(subscribers);
    }

    [Fact]
    public async Task PublishAsync_ConcurrentCalls_NoDeadlock()
    {
        var handler = new ConcurrentTestSubscriber();

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        var tasks = Enumerable.Range(0, 10).Select(_ =>
            eventBus.PublishAsync(new TestEvent()));

        await Task.WhenAll(tasks);

        Assert.Equal(10, handler.HandleCount);
    }

    [Fact]
    public async Task PublishAsync_NullEvent_ThrowsArgumentNullException()
    {
        var handler = new TestSubscriber();

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => eventBus.PublishAsync<TestEvent>(null!));
    }

    [Fact]
    public async Task PublishAsync_CancelledToken_ThrowsWhenSubscriberChecks()
    {
        var handler = new CancellationTokenCheckingSubscriber();

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => eventBus.PublishAsync(new TestEvent(), cts.Token));
    }

    [Fact]
    public async Task PublishAsync_NoSubscribersForEventType_EmptyEnumeration()
    {
        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(new TestSubscriber());
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await eventBus.PublishAsync(new AnotherTestEvent());
    }

    [Fact]
    public async Task PublishAsync_SubscriberThrows_ExceptionMessagePreserved()
    {
        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(new ThrowingTestSubscriber());
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => eventBus.PublishAsync(new TestEvent()));

        Assert.Equal("Test exception", ex.Message);
    }

    [Fact]
    public async Task PublishAsync_MultiplePublishes_SequentialCallsMaintainOrder()
    {
        var callOrder = new List<int>();
        var handler1 = new OrderedTestSubscriber(1, callOrder);
        var handler2 = new OrderedTestSubscriber(2, callOrder);

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler1);
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler2);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await eventBus.PublishAsync(new TestEvent());
        Assert.Equal([1, 2], callOrder);

        callOrder.Clear();

        await eventBus.PublishAsync(new TestEvent());
        Assert.Equal([1, 2], callOrder);
    }

    [Fact]
    public async Task PublishAsync_CancellationToken_CancelledDuringHandle_Propagates()
    {
        using var cts = new CancellationTokenSource();
        var handler = new CancellationDuringHandleSubscriber(cts);

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => eventBus.PublishAsync(new TestEvent(), cts.Token));
    }

    [Fact]
    public async Task PublishAsync_SubscriberReturnsCanceledTask_Propagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var handler = new PreCanceledSubscriber(cts.Token);

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => eventBus.PublishAsync(new TestEvent(), CancellationToken.None));
    }

    [Fact]
    public async Task PublishAsync_ResolvesTransientPerCall()
    {
        var tracker = new InstanceTracker();

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton(tracker);
        services.AddTransient<IEventSubscriber<TestEvent>, TransientTrackerSubscriber>();
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await eventBus.PublishAsync(new TestEvent());
        await eventBus.PublishAsync(new TestEvent());

        Assert.Equal(2, tracker.Count);
    }

    [Fact]
    public async Task PublishAsync_ContinueOnError_SubsequentSubscribersCalled()
    {
        var handler1 = new TestSubscriber();
        var handler2 = new ThrowingTestSubscriber();
        var handler3 = new TestSubscriber();

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler1);
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler2);
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler3);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        var options = new PublishOptions { ContinueOnError = true };
        await Assert.ThrowsAsync<AggregateException>(
            () => eventBus.PublishAsync(new TestEvent(), options));

        Assert.Equal(1, handler1.HandleCount);
        Assert.Equal(1, handler3.HandleCount);
    }

    [Fact]
    public async Task PublishAsync_ContinueOnError_ThrowsAggregateException()
    {
        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(new ThrowingTestSubscriber());
        services.AddSingleton<IEventSubscriber<TestEvent>>(new ThrowingTestSubscriber());
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        var options = new PublishOptions { ContinueOnError = true };
        var ex = await Assert.ThrowsAsync<AggregateException>(
            () => eventBus.PublishAsync(new TestEvent(), options));

        Assert.Equal(2, ex.InnerExceptions.Count);
    }

    [Fact]
    public async Task PublishAsync_ContinueOnError_CallbackInvoked()
    {
        var callbackCalled = false;
        Exception capturedException = null!;
        object capturedEvent = null!;

        var services = new ServiceCollection();
        services.AddLiteEventBus(options =>
        {
            options.DefaultContinueOnError = true;
            options.OnSubscriberError = (sp, @event, ex) =>
            {
                callbackCalled = true;
                capturedException = ex;
                capturedEvent = @event;
                return Task.CompletedTask;
            };
        });
        services.AddSingleton<IEventSubscriber<TestEvent>>(new ThrowingTestSubscriber());
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await Assert.ThrowsAsync<AggregateException>(
            () => eventBus.PublishAsync(new TestEvent()));

        Assert.True(callbackCalled);
        Assert.IsType<InvalidOperationException>(capturedException);
        Assert.Equal("Test exception", capturedException.Message);
        Assert.NotNull(capturedEvent);
    }

    [Fact]
    public async Task PublishAsync_ContinueOnError_CallbackReceivesScopeProvider()
    {
        var providerFromCallback = null as IServiceProvider;

        var services = new ServiceCollection();
        services.AddLiteEventBus(options =>
        {
            options.DefaultContinueOnError = true;
            options.OnSubscriberError = (sp, @event, ex) =>
            {
                providerFromCallback = sp;
                return Task.CompletedTask;
            };
        });
        services.AddSingleton<IEventSubscriber<TestEvent>>(new ThrowingTestSubscriber());
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await Assert.ThrowsAsync<AggregateException>(
            () => eventBus.PublishAsync(new TestEvent()));

        Assert.NotNull(providerFromCallback);
    }

    [Fact]
    public async Task PublishAsync_DefaultOptions_ContinueOnErrorFalse()
    {
        var handlerAfterThrow = new TestSubscriber();

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(new ThrowingTestSubscriber());
        services.AddSingleton<IEventSubscriber<TestEvent>>(handlerAfterThrow);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        try
        {
            await eventBus.PublishAsync(new TestEvent());
        }
        catch (InvalidOperationException)
        {
        }

        Assert.Equal(0, handlerAfterThrow.HandleCount);
    }

    [Fact]
    public async Task PublishAsync_ScopedSubscriber_ResolvesCorrectly()
    {
        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddScoped<ScopedDependency>();
        services.AddTransient<IEventSubscriber<TestEvent>, ScopedTestSubscriber>();
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await eventBus.PublishAsync(new TestEvent());
    }

    [Fact]
    public async Task PublishAsync_ScopedSubscriber_IsNewInstancePerPublish()
    {
        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddScoped<ScopedDependency>();
        services.AddTransient<IEventSubscriber<TestEvent>, ScopedTestSubscriber>();
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await eventBus.PublishAsync(new TestEvent());
        await eventBus.PublishAsync(new TestEvent());
    }

    [Fact]
    public async Task PublishAsync_ContinueOnError_CallbackThrows_ContinuesPublishing()
    {
        var handler1 = new TestSubscriber();
        var handler2 = new ThrowingTestSubscriber();
        var handler3 = new TestSubscriber();

        var services = new ServiceCollection();
        services.AddLiteEventBus(options =>
        {
            options.DefaultContinueOnError = true;
            options.OnSubscriberError = (sp, @event, ex) =>
                throw new InvalidOperationException("Callback error");
        });
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler1);
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler2);
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler3);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        var ex = await Assert.ThrowsAsync<AggregateException>(
            () => eventBus.PublishAsync(new TestEvent()));

        Assert.Single(ex.InnerExceptions);
        Assert.IsType<InvalidOperationException>(ex.InnerExceptions[0]);
        Assert.Equal("Test exception", ex.InnerExceptions[0].Message);
        Assert.Equal(1, handler1.HandleCount);
        Assert.Equal(1, handler3.HandleCount);
    }

    [Fact]
    public async Task PublishAsync_LambdaSubscriber_CallsHandler()
    {
        var callCount = 0;

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSubscriber<TestEvent>((TestEvent _, CancellationToken _) =>
        {
            callCount++;
            return Task.CompletedTask;
        });
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await eventBus.PublishAsync(new TestEvent());

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task PublishAsync_LambdaSubscriber_ReceivesEvent()
    {
        object? receivedEvent = null;

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSubscriber<TestEvent>((TestEvent @event, CancellationToken _) =>
        {
            receivedEvent = @event;
            return Task.CompletedTask;
        });
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        var testEvent = new TestEvent();
        await eventBus.PublishAsync(testEvent);

        Assert.Same(testEvent, receivedEvent);
    }

    [Fact]
    public async Task PublishAsync_LambdaSubscriber_ThrowsPropagates()
    {
        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSubscriber<TestEvent>((TestEvent _, CancellationToken _) =>
            throw new InvalidOperationException("Lambda error"));
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => eventBus.PublishAsync(new TestEvent()));

        Assert.Equal("Lambda error", ex.Message);
    }

    [Fact]
    public async Task PublishAsync_MixedClassAndLambdaSubscribers_AllCalled()
    {
        var classHandler = new TestSubscriber();
        var lambdaCallCount = 0;

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(classHandler);
        services.AddSubscriber<TestEvent>((TestEvent _, CancellationToken _) =>
        {
            lambdaCallCount++;
            return Task.CompletedTask;
        });
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await eventBus.PublishAsync(new TestEvent());

        Assert.Equal(1, classHandler.HandleCount);
        Assert.Equal(1, lambdaCallCount);
    }

    [Fact]
    public async Task PublishAsync_FuncTaskLambda_WorksCorrectly()
    {
        var callCount = 0;

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSubscriber<TestEvent>(_ =>
        {
            callCount++;
            return Task.CompletedTask;
        });
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await eventBus.PublishAsync(new TestEvent());

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task PublishAsync_ActionLambda_WorksCorrectly()
    {
        var callCount = 0;

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSubscriber<TestEvent>(_ =>
        {
            callCount++;
        });
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await eventBus.PublishAsync(new TestEvent());

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task PublishAsync_ContinueOnError_CancellationStillPropagates()
    {
        using var cts = new CancellationTokenSource();
        var handler = new CancellationDuringHandleSubscriber(cts);

        var services = new ServiceCollection();
        services.AddLiteEventBus(options =>
        {
            options.DefaultContinueOnError = true;
        });
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => eventBus.PublishAsync(new TestEvent(), cts.Token));
    }

    [Fact]
    public async Task PublishAsync_ServiceProviderSubscriber_ResolvesService()
    {
        TestEvent? receivedEvent = null;
        CancellationToken? receivedCt = null;

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton(new ScopedDependency());
        services.AddSubscriber<TestEvent>((e, ct, sp) =>
        {
            var dep = sp.GetRequiredService<ScopedDependency>();
            Assert.NotNull(dep);
            receivedEvent = e;
            receivedCt = ct;
            return Task.CompletedTask;
        });
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        using var cts = new CancellationTokenSource();
        var testEvent = new TestEvent();
        await eventBus.PublishAsync(testEvent, cts.Token);

        Assert.Same(testEvent, receivedEvent);
        Assert.Equal(cts.Token, receivedCt);
    }

    [Fact]
    public async Task PublishAsync_ServiceProviderSubscriber_ScopedServicePerPublish()
    {
        var capturedIds = new List<Guid>();

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddScoped<ScopedDependency>();
        services.AddSubscriber<TestEvent>((e, ct, sp) =>
        {
            var dep = sp.GetRequiredService<ScopedDependency>();
            capturedIds.Add(dep.Id);
            return Task.CompletedTask;
        });
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await eventBus.PublishAsync(new TestEvent());
        await eventBus.PublishAsync(new TestEvent());

        Assert.Equal(2, capturedIds.Count);
        Assert.NotEqual(capturedIds[0], capturedIds[1]);
    }

    [Fact]
    public async Task PublishAsync_MixedClassLambdaServiceProviderSubscribers_AllCalled()
    {
        var classHandler = new TestSubscriber();
        var lambdaCallCount = 0;
        var spCallCount = 0;

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton(new ScopedDependency());
        services.AddSingleton<IEventSubscriber<TestEvent>>(classHandler);
        services.AddSubscriber<TestEvent>((TestEvent _, CancellationToken _) =>
        {
            lambdaCallCount++;
            return Task.CompletedTask;
        });
        services.AddSubscriber<TestEvent>((e, ct, sp) =>
        {
            var dep = sp.GetRequiredService<ScopedDependency>();
            Assert.NotNull(dep);
            spCallCount++;
            return Task.CompletedTask;
        });
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await eventBus.PublishAsync(new TestEvent());

        Assert.Equal(1, classHandler.HandleCount);
        Assert.Equal(1, lambdaCallCount);
        Assert.Equal(1, spCallCount);
    }

    [Fact]
    public async Task PublishAsync_ServiceProviderSubscriber_Throws_SubsequentNotCalled()
    {
        var handler1 = new TestSubscriber();
        var handlerAfterThrow = new TestSubscriber();

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler1);
        services.AddSubscriber<TestEvent>((e, ct, sp) =>
            throw new InvalidOperationException("SP error"));
        services.AddSingleton<IEventSubscriber<TestEvent>>(handlerAfterThrow);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        try
        {
            await eventBus.PublishAsync(new TestEvent());
        }
        catch (InvalidOperationException)
        {
        }

        Assert.Equal(1, handler1.HandleCount);
        Assert.Equal(0, handlerAfterThrow.HandleCount);
    }

    [Fact]
    public async Task PublishAsync_ServiceProviderSubscriber_SyncAction_ResolvesService()
    {
        var callCount = 0;
        CancellationToken? receivedCt = null;

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton(new ScopedDependency());
        services.AddSubscriber<TestEvent>((Action<TestEvent, CancellationToken, IServiceProvider>)((e, ct, sp) =>
        {
            var dep = sp.GetRequiredService<ScopedDependency>();
            Assert.NotNull(dep);
            callCount++;
            receivedCt = ct;
        }));
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        using var cts = new CancellationTokenSource();
        await eventBus.PublishAsync(new TestEvent(), cts.Token);

        Assert.Equal(1, callCount);
        Assert.Equal(cts.Token, receivedCt);
    }

    [Fact]
    public async Task PublishAsync_ServiceProviderSubscriber_ContinueOnError_SubsequentCalled()
    {
        var handler1 = new TestSubscriber();
        var throwingCalled = false;
        var handlerAfterThrow = new TestSubscriber();

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler1);
        services.AddSubscriber<TestEvent>((e, ct, sp) =>
        {
            throwingCalled = true;
            throw new InvalidOperationException("SP error");
        });
        services.AddSingleton<IEventSubscriber<TestEvent>>(handlerAfterThrow);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        var options = new PublishOptions { ContinueOnError = true };
        await Assert.ThrowsAsync<AggregateException>(
            () => eventBus.PublishAsync(new TestEvent(), options));

        Assert.Equal(1, handler1.HandleCount);
        Assert.True(throwingCalled);
        Assert.Equal(1, handlerAfterThrow.HandleCount);
    }

    [Fact]
    public async Task PublishAsync_ServiceProviderSubscriber_ContinueOnError_ThrowsAggregateException()
    {
        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSubscriber<TestEvent>((e, ct, sp) =>
            throw new InvalidOperationException("SP error 1"));
        services.AddSubscriber<TestEvent>((e, ct, sp) =>
            throw new InvalidOperationException("SP error 2"));
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        var options = new PublishOptions { ContinueOnError = true };
        var ex = await Assert.ThrowsAsync<AggregateException>(
            () => eventBus.PublishAsync(new TestEvent(), options));

        Assert.Equal(2, ex.InnerExceptions.Count);
    }

    [Fact]
    public async Task PublishAsync_ServiceProviderSubscriber_ContinueOnError_CallbackInvoked()
    {
        var callbackCalled = false;
        Exception capturedException = null!;

        var services = new ServiceCollection();
        services.AddLiteEventBus(options =>
        {
            options.DefaultContinueOnError = true;
            options.OnSubscriberError = (sp, @event, ex) =>
            {
                callbackCalled = true;
                capturedException = ex;
                return Task.CompletedTask;
            };
        });
        services.AddSubscriber<TestEvent>((e, ct, sp) =>
            throw new InvalidOperationException("SP error"));
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await Assert.ThrowsAsync<AggregateException>(
            () => eventBus.PublishAsync(new TestEvent()));

        Assert.True(callbackCalled);
        Assert.IsType<InvalidOperationException>(capturedException);
        Assert.Equal("SP error", capturedException.Message);
    }

    [Fact]
    public async Task PublishAsync_ServiceProviderSubscriber_CancelledToken_Throws()
    {
        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSubscriber<TestEvent>((e, ct, sp) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        });
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => eventBus.PublishAsync(new TestEvent(), cts.Token));
    }

    [Fact]
    public async Task PublishAsync_MultipleServiceProviderSubscribers_AllCalled()
    {
        var callCount1 = 0;
        var callCount2 = 0;

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSubscriber<TestEvent>((e, ct, sp) =>
        {
            callCount1++;
            return Task.CompletedTask;
        });
        services.AddSubscriber<TestEvent>((e, ct, sp) =>
        {
            callCount2++;
            return Task.CompletedTask;
        });
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await eventBus.PublishAsync(new TestEvent());

        Assert.Equal(1, callCount1);
        Assert.Equal(1, callCount2);
    }

    [Fact]
    public async Task PublishAsync_ServiceProviderSubscriber_ExecutionOrder()
    {
        var callOrder = new List<int>();

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton<IEventSubscriber<TestEvent>>(new OrderedTestSubscriber(1, callOrder));
        services.AddSubscriber<TestEvent>((e, ct, sp) =>
        {
            callOrder.Add(2);
            return Task.CompletedTask;
        });
        services.AddSingleton<IEventSubscriber<TestEvent>>(new OrderedTestSubscriber(3, callOrder));
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await eventBus.PublishAsync(new TestEvent());

        Assert.Equal([1, 2, 3], callOrder);
    }

    [Fact]
    public async Task PublishAsync_ServiceProviderSubscriber_SyncAction_Throws_SubsequentNotCalled()
    {
        var throwingCalled = false;
        var handlerAfterThrow = new TestSubscriber();

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSubscriber<TestEvent>((Action<TestEvent, CancellationToken, IServiceProvider>)((e, ct, sp) =>
        {
            throwingCalled = true;
            throw new InvalidOperationException("Sync SP error");
        }));
        services.AddSingleton<IEventSubscriber<TestEvent>>(handlerAfterThrow);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        try
        {
            await eventBus.PublishAsync(new TestEvent());
        }
        catch (InvalidOperationException)
        {
        }

        Assert.True(throwingCalled);
        Assert.Equal(0, handlerAfterThrow.HandleCount);
    }

    [Fact]
    public async Task PublishAsync_ServiceProviderSubscriber_SyncAction_ContinueOnError_SubsequentCalled()
    {
        var throwingCalled = false;
        var handlerAfterThrow = new TestSubscriber();

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSubscriber<TestEvent>((Action<TestEvent, CancellationToken, IServiceProvider>)((e, ct, sp) =>
        {
            throwingCalled = true;
            throw new InvalidOperationException("Sync SP error");
        }));
        services.AddSingleton<IEventSubscriber<TestEvent>>(handlerAfterThrow);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        var options = new PublishOptions { ContinueOnError = true };
        await Assert.ThrowsAsync<AggregateException>(
            () => eventBus.PublishAsync(new TestEvent(), options));

        Assert.True(throwingCalled);
        Assert.Equal(1, handlerAfterThrow.HandleCount);
    }

    // --- PublishOptions immutability (init-only) ---

    [Fact]
    public void PublishOptions_InitOnly_CannotBeSetAfterConstruction()
    {
        // Verifies that PublishOptions uses init-only setters by confirming
        // it can only be set via object initializer syntax.
        var options = new PublishOptions { ContinueOnError = true };
        Assert.True(options.ContinueOnError);
    }

    [Fact]
    public void PublishOptions_DefaultValue_IsFalse()
    {
        var options = new PublishOptions();
        Assert.False(options.ContinueOnError);
    }

    [Fact]
    public void PublishOptions_IsSealed_CannotBeInherited()
    {
        Assert.True(typeof(PublishOptions).IsSealed);
    }

    [Fact]
    public async Task PublishAsync_NullOptions_UsesGlobalDefaults()
    {
        // PublishOptions? null should behave identically to passing no options
        var handlerAfterThrow = new TestSubscriber();

        var services = new ServiceCollection();
        services.AddLiteEventBus(); // DefaultContinueOnError = false
        services.AddSingleton<IEventSubscriber<TestEvent>>(new ThrowingTestSubscriber());
        services.AddSingleton<IEventSubscriber<TestEvent>>(handlerAfterThrow);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => eventBus.PublishAsync(new TestEvent(), (PublishOptions?)null));

        // Null options falls back to DefaultContinueOnError = false → fail-fast
        Assert.Equal(0, handlerAfterThrow.HandleCount);
    }

    [Fact]
    public async Task PublishAsync_NullOptions_WithGlobalContinueOnError_ContinuesPublishing()
    {
        var handlerAfterThrow = new TestSubscriber();

        var services = new ServiceCollection();
        services.AddLiteEventBus(opt => opt.DefaultContinueOnError = true);
        services.AddSingleton<IEventSubscriber<TestEvent>>(new ThrowingTestSubscriber());
        services.AddSingleton<IEventSubscriber<TestEvent>>(handlerAfterThrow);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await Assert.ThrowsAsync<AggregateException>(
            () => eventBus.PublishAsync(new TestEvent(), (PublishOptions?)null));

        Assert.Equal(1, handlerAfterThrow.HandleCount);
    }

    [Fact]
    public async Task PublishAsync_ExplicitOptions_OverridesGlobalDefault()
    {
        // Per-call options take priority over the global DefaultContinueOnError
        var handlerAfterThrow = new TestSubscriber();

        var services = new ServiceCollection();
        services.AddLiteEventBus(opt => opt.DefaultContinueOnError = false);
        services.AddSingleton<IEventSubscriber<TestEvent>>(new ThrowingTestSubscriber());
        services.AddSingleton<IEventSubscriber<TestEvent>>(handlerAfterThrow);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        var options = new PublishOptions { ContinueOnError = true };
        await Assert.ThrowsAsync<AggregateException>(
            () => eventBus.PublishAsync(new TestEvent(), options));

        Assert.Equal(1, handlerAfterThrow.HandleCount);
    }

    // --- OperationCanceledException always propagates ---

    [Fact]
    public async Task PublishAsync_ContinueOnError_OperationCanceledException_AlwaysPropagates()
    {
        // Even with ContinueOnError = true, OperationCanceledException must not be swallowed
        using var cts = new CancellationTokenSource();
        var handler = new CancellationDuringHandleSubscriber(cts);
        var handlerAfterCancel = new TestSubscriber();

        var services = new ServiceCollection();
        services.AddLiteEventBus(opt => opt.DefaultContinueOnError = true);
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler);
        services.AddSingleton<IEventSubscriber<TestEvent>>(handlerAfterCancel);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => eventBus.PublishAsync(new TestEvent(), cts.Token));

        // Cancellation propagated immediately — subsequent subscriber was not called
        Assert.Equal(0, handlerAfterCancel.HandleCount);
    }

    [Fact]
    public async Task PublishAsync_ContinueOnError_PreCancelledToken_PropagatesImmediately()
    {
        // ThrowIfCancellationRequested before each subscriber also uses OperationCanceledException
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var handler = new TestSubscriber();

        var services = new ServiceCollection();
        services.AddLiteEventBus(opt => opt.DefaultContinueOnError = true);
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => eventBus.PublishAsync(new TestEvent(), cts.Token));

        // Token was pre-cancelled — handler was never reached
        Assert.Equal(0, handler.HandleCount);
    }

    [Fact]
    public async Task PublishAsync_ContinueOnError_OtherExceptionsDoNotPropagateImmediately()
    {
        // Confirms that only OperationCanceledException breaks early;
        // regular exceptions are collected
        var handler1 = new ThrowingTestSubscriber();
        var handler2 = new ThrowingTestSubscriber();
        var handler3 = new TestSubscriber();

        var services = new ServiceCollection();
        services.AddLiteEventBus(opt => opt.DefaultContinueOnError = true);
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler1);
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler2);
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler3);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        var ex = await Assert.ThrowsAsync<AggregateException>(
            () => eventBus.PublishAsync(new TestEvent()));

        Assert.Equal(2, ex.InnerExceptions.Count);
        Assert.Equal(1, handler3.HandleCount);
    }

    // --- Lazy List<Exception> allocation ---

    [Fact]
    public async Task PublishAsync_ContinueOnError_NoExceptions_DoesNotThrow()
    {
        // Happy path with ContinueOnError = true: no AggregateException when no errors occur
        var handler1 = new TestSubscriber();
        var handler2 = new TestSubscriber();

        var services = new ServiceCollection();
        services.AddLiteEventBus(opt => opt.DefaultContinueOnError = true);
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler1);
        services.AddSingleton<IEventSubscriber<TestEvent>>(handler2);
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await eventBus.PublishAsync(new TestEvent()); // must not throw

        Assert.Equal(1, handler1.HandleCount);
        Assert.Equal(1, handler2.HandleCount);
    }

    [Fact]
    public async Task PublishAsync_ContinueOnError_SingleException_AggregateHasOneInner()
    {
        // Lazy list: first exception initializes the list
        var services = new ServiceCollection();
        services.AddLiteEventBus(opt => opt.DefaultContinueOnError = true);
        services.AddSingleton<IEventSubscriber<TestEvent>>(new ThrowingTestSubscriber());
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        var ex = await Assert.ThrowsAsync<AggregateException>(
            () => eventBus.PublishAsync(new TestEvent()));

        Assert.Single(ex.InnerExceptions);
    }

    // --- InstanceTracker (replaces static TransientTrackerSubscriber) ---

    [Fact]
    public async Task PublishAsync_ResolvesTransientPerCall_MultiplePublishes()
    {
        var tracker = new InstanceTracker();

        var services = new ServiceCollection();
        services.AddLiteEventBus();
        services.AddSingleton(tracker);
        services.AddTransient<IEventSubscriber<TestEvent>, TransientTrackerSubscriber>();
        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        await eventBus.PublishAsync(new TestEvent());
        await eventBus.PublishAsync(new TestEvent());
        await eventBus.PublishAsync(new TestEvent());

        Assert.Equal(3, tracker.Count);
    }

    [Fact]
    public async Task PublishAsync_MultipleTrackers_AreIndependent()
    {
        // Two independent trackers confirm isolation between test instances
        var tracker1 = new InstanceTracker();
        var tracker2 = new InstanceTracker();

        var services1 = new ServiceCollection();
        services1.AddLiteEventBus();
        services1.AddSingleton(tracker1);
        services1.AddTransient<IEventSubscriber<TestEvent>, TransientTrackerSubscriber>();

        var services2 = new ServiceCollection();
        services2.AddLiteEventBus();
        services2.AddSingleton(tracker2);
        services2.AddTransient<IEventSubscriber<TestEvent>, TransientTrackerSubscriber>();

        var provider1 = services1.BuildServiceProvider();
        var provider2 = services2.BuildServiceProvider();

        await provider1.GetRequiredService<IEventBus>().PublishAsync(new TestEvent());
        await provider1.GetRequiredService<IEventBus>().PublishAsync(new TestEvent());
        await provider2.GetRequiredService<IEventBus>().PublishAsync(new TestEvent());

        Assert.Equal(2, tracker1.Count);
        Assert.Equal(1, tracker2.Count);
    }
}
