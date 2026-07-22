# Real-World Examples

## ASP.NET Core Web API

Integrate LiteEventBus into an ASP.NET Core application for handling domain events after command execution.

### 1. Define events and subscribers

```csharp
// Events/OrderSubmitted.cs
public sealed record OrderSubmitted(
    Guid OrderId,
    string CustomerEmail,
    decimal Total);

// Subscribers/NotifyCustomer.cs
public sealed class NotifyCustomer : IEventSubscriber<OrderSubmitted>
{
    private readonly IEmailService _email;
    private readonly ILogger<NotifyCustomer> _logger;

    public NotifyCustomer(IEmailService email, ILogger<NotifyCustomer> logger)
    {
        _email = email;
        _logger = logger;
    }

    public async Task HandleAsync(OrderSubmitted @event, CancellationToken ct)
    {
        _logger.LogInformation("Sending confirmation for order {OrderId}", @event.OrderId);
        await _email.SendAsync(
            @event.CustomerEmail,
            "Your order has been confirmed!",
            ct);
    }
}

// Subscribers/UpdateInventory.cs
public sealed class UpdateInventory : IEventSubscriber<OrderSubmitted>
{
    private readonly AppDbContext _db;
    private readonly ILogger<UpdateInventory> _logger;

    public UpdateInventory(AppDbContext db, ILogger<UpdateInventory> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task HandleAsync(OrderSubmitted @event, CancellationToken ct)
    {
        _logger.LogInformation("Updating inventory for order {OrderId}", @event.OrderId);
        // ... inventory logic ...
        await _db.SaveChangesAsync(ct);
    }
}
```

### 2. Register in `Program.cs`

```csharp
using LiteEventBus.Abstractions;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Register LiteEventBus with production defaults
builder.Services.AddLiteEventBus(options =>
{
    options.DefaultContinueOnError = true;
    options.OnSubscriberError = async (sp, @event, exception) =>
    {
        var logger = sp.GetRequiredService<ILogger<IEventBus>>();
        logger.LogError(exception,
            "Subscriber failed for {EventType}",
            @event.GetType().Name);
    };
});

// Register subscribers
builder.Services.AddSubscriber<OrderSubmitted, NotifyCustomer>();
builder.Services.AddSubscriber<OrderSubmitted, UpdateInventory>();

// Register infrastructure
builder.Services.AddDbContext<AppDbContext>(...);
builder.Services.AddScoped<IEmailService, SmtpEmailService>();

var app = builder.Build();
```

### 3. Use in a controller

```csharp
[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IEventBus _eventBus;

    public OrdersController(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder(CreateOrderRequest request)
    {
        // Business logic...
        var orderId = Guid.NewGuid();

        // Publish domain event
        await _eventBus.PublishAsync(new OrderSubmitted(
            orderId,
            request.Email,
            request.Total));

        return Ok(new { OrderId = orderId });
    }
}
```

---

## Background Service with LiteEventBus

Use LiteEventBus inside an `IHostedService` for background processing.

```csharp
public sealed class OrderProcessingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderProcessingWorker> _logger;

    public OrderProcessingWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<OrderProcessingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Simulate processing a queue
            await Task.Delay(5000, stoppingToken);

            using var scope = _scopeFactory.CreateScope();
            var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

            try
            {
                await eventBus.PublishAsync(
                    new OrderSubmitted(Guid.NewGuid(), "test@example.com", 99.99m),
                    stoppingToken);
            }
            catch (AggregateException ex)
            {
                foreach (var inner in ex.InnerExceptions)
                {
                    _logger.LogError(inner, "Order processing failed");
                }
            }
        }
    }
}
```

Registration:

```csharp
builder.Services.AddHostedService<OrderProcessingWorker>();
```

---

## Structured Error Logging with Serilog

Combine `OnSubscriberError` with Serilog for structured error logging.

```csharp
builder.Services.AddLiteEventBus(options =>
{
    options.DefaultContinueOnError = true;
    options.OnSubscriberError = (sp, @event, exception) =>
    {
        Log.Error(exception,
            "Subscriber error processing {EventType} {@Event}",
            @event.GetType().Name,
            @event);
        return Task.CompletedTask;
    };
});
```

The callback receives the raw event object, enabling structured log properties for search and analysis.

---

## Subscriber with HttpClient

Subscribers can depend on `IHttpClientFactory` for outgoing HTTP calls.

```csharp
public sealed class ForwardOrderToLegacySystem : IEventSubscriber<OrderSubmitted>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ForwardOrderToLegacySystem> _logger;

    public ForwardOrderToLegacySystem(
        IHttpClientFactory httpClientFactory,
        ILogger<ForwardOrderToLegacySystem> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task HandleAsync(OrderSubmitted @event, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("LegacySystem");

        var response = await client.PostAsJsonAsync("/api/orders", @event, ct);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Order {OrderId} forwarded to legacy system", @event.OrderId);
    }
}
```

Registration:

```csharp
builder.Services.AddHttpClient("LegacySystem", client =>
{
    client.BaseAddress = new Uri("https://legacy.example.com");
});

builder.Services.AddSubscriber<OrderSubmitted, ForwardOrderToLegacySystem>();
```
