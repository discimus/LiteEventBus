# LiteEventBus

Uma biblioteca .NET leve para comunicação **Publish/Subscribe em memória**, com foco em simplicidade, baixo overhead e integração nativa com `Microsoft.Extensions.DependencyInjection`.

## Objetivo

LiteEventBus permite que publicadores emitam eventos fortemente tipados e que múltiplos assinantes sejam notificados de forma assíncrona. A biblioteca **não** implementa Mediator, Command Bus, CQRS, Event Sourcing ou mensageria distribuída.

## Instalação

```shell
dotnet add package LiteEventBus
```

## Primeiros passos

### 1. Definir um evento

```csharp
using LiteEventBus.Abstractions;

public sealed record UserRegistered(
    Guid UserId,
    string Email,
    string FullName) : IEvent;
```

### 2. Criar subscribers

```csharp
using LiteEventBus.Abstractions;

public sealed class SendWelcomeEmail : IEventSubscriber<UserRegistered>
{
    public Task HandleAsync(UserRegistered @event, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Welcome email sent to {@event.Email}");
        return Task.CompletedTask;
    }
}

public sealed class LogAuditEntry : IEventSubscriber<UserRegistered>
{
    public Task HandleAsync(UserRegistered @event, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Audit: user {@event.UserId} registered");
        return Task.CompletedTask;
    }
}
```

### 3. Registrar na DI

```csharp
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddLiteEventBus();
services.AddSubscriber<UserRegistered, SendWelcomeEmail>();
services.AddSubscriber<UserRegistered, LogAuditEntry>();
var provider = services.BuildServiceProvider();
```

### 4. Publicar um evento

```csharp
var eventBus = provider.GetRequiredService<IEventBus>();

await eventBus.PublishAsync(
    new UserRegistered(
        Guid.NewGuid(),
        "john.doe@example.com",
        "John Doe"));
```

## Tratamento de erros

### Comportamento padrão (fail-fast)

Por padrão, a publicação é interrompida na primeira exceção. Subscribers registrados após o que falhou não executam:

```csharp
await eventBus.PublishAsync(new UserRegistered(Guid.NewGuid(), "a@b.com", "A"));
// Se o 2º subscriber lançar exceção, o 3º não executa
```

### ContinueOnError (por chamada)

Para executar todos os subscribers mesmo em caso de erro, use `PublishOptions`:

```csharp
var options = new PublishOptions { ContinueOnError = true };

try
{
    await eventBus.PublishAsync(new UserRegistered(Guid.NewGuid(), "a@b.com", "A"), options);
}
catch (AggregateException ex)
{
    // ex.InnerExceptions contém as exceções de todos os subscribers que falharam
    foreach (var inner in ex.InnerExceptions)
    {
        Console.WriteLine($"Subscriber error: {inner.Message}");
    }
}
```

### Configuração global

Defina o comportamento padrão para todas as publicações durante o registro na DI:

```csharp
services.AddLiteEventBus(options =>
{
    options.DefaultContinueOnError = true;
});
```

A configuração global é usada quando `PublishAsync` é chamado sem `PublishOptions`. O valor por chamada sempre sobrescreve o global.

### Callback de erro

Registre um callback para ser notificado quando um subscriber falha (útil para logging ou métricas):

```csharp
services.AddLiteEventBus(options =>
{
    options.DefaultContinueOnError = true;
    options.OnSubscriberError = async (serviceProvider, @event, exception) =>
    {
        var logger = serviceProvider.GetRequiredService<ILogger<IEventBus>>();
        logger.LogError(exception, "Subscriber falhou ao processar {EventType}", @event.GetType().Name);
    };
});
```

O callback recebe o `IServiceProvider` do escopo atual, permitindo resolver serviços scoped (loggers, etc.).

## Subscribers com dependências scoped

Cada chamada de `PublishAsync` cria um escopo DI, permitindo que subscribers consumam dependências scoped como `DbContext` do Entity Framework Core:

```csharp
using LiteEventBus.Abstractions;

public sealed class UserRegisteredHandler : IEventSubscriber<UserRegistered>
{
    private readonly AppDbContext _db;

    public UserRegisteredHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task HandleAsync(UserRegistered @event, CancellationToken cancellationToken)
    {
        _db.Users.Add(new User(@event.UserId, @event.Email, @event.FullName));
        await _db.SaveChangesAsync(cancellationToken);
    }
}
```

Registro na DI:

```csharp
services.AddDbContext<AppDbContext>(...);
services.AddLiteEventBus();
services.AddSubscriber<UserRegistered, UserRegisteredHandler>();
```

## API

### `IEvent`

Interface marcadora para todos os eventos.

### `IEventSubscriber<TEvent>`

Contrato para subscribers. Deve ser implementado por cada assinante de evento.

### `IEventBus`

Contrato para publicação de eventos.

```csharp
Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
    where TEvent : IEvent;

Task PublishAsync<TEvent>(TEvent @event, PublishOptions options, CancellationToken cancellationToken = default)
    where TEvent : IEvent;
```

### `PublishOptions`

Configura o comportamento de uma publicação específica.

| Propriedade | Tipo | Padrão | Descrição |
|-------------|------|--------|-----------|
| `ContinueOnError` | `bool` | `false` | Quando `true`, todos subscribers executam mesmo em caso de erro. As exceções são coletadas e um `AggregateException` é lançado ao final. |

### `EventBusOptions`

Configura o comportamento global do LiteEventBus durante o registro na DI.

| Propriedade | Tipo | Padrão | Descrição |
|-------------|------|--------|-----------|
| `DefaultContinueOnError` | `bool` | `false` | Valor global usado quando `PublishAsync` é chamado sem `PublishOptions`. |
| `OnSubscriberError` | `Func<IServiceProvider, IEvent, Exception, Task>?` | `null` | Callback invocado quando um subscriber falha e `ContinueOnError` é `true`. |

### `IServiceCollection.AddLiteEventBus()`

```csharp
// Registro padrão
services.AddLiteEventBus();

// Com configuração global
services.AddLiteEventBus(options => { ... });
```

Registra `IEventBus` como singleton. É idempotente: chamadas múltiplas não criam registros duplicados.

### `IServiceCollection.AddSubscriber<TEvent, TSubscriber>()`

Registra um subscriber como transient. Ignora silenciosamente registros duplicados do mesmo par `(TEvent, TSubscriber)`.

## Comportamento

- Subscribers são executados **sequencialmente** na ordem de registro.
- Por padrão, a primeira exceção interrompe a publicação e é propagada imediatamente.
- Com `ContinueOnError = true`, todos subscribers executam. Exceções são coletadas e um `AggregateException` é lançado ao final. O callback `OnSubscriberError` é invocado para cada falha.
- Cada chamada de `PublishAsync` cria um escopo DI. Subscribers podem consumir dependências scoped (`DbContext`, `HttpContext`, etc.).
- Subscribers são resolvidos via DI como **transient** a cada chamada de `PublishAsync`.
- O `IEventBus` é registrado como **singleton**.
- `AddSubscriber` ignora registros duplicados do mesmo tipo de subscriber para o mesmo evento.
- `ConfigureAwait(false)` é utilizado em todo código interno da biblioteca.

## Limitações

- Apenas comunicação em memória.
- Sem suporte a mensageria distribuída (RabbitMQ, Kafka, Azure Service Bus, etc.).
- Sem suporte a Mediator, CQRS ou Command Bus.
- Sem pipeline behaviors ou middleware.
- Sem retry, dead letter ou persistência.
- Sem reflection scanning ou source generators.
- Apenas API assíncrona.

## Design principles

- **KISS** — a solução mais simples possível.
- **YAGNI** — nada além do necessário.
- **Zero configuração** — apenas registrar na DI.
- **Thread-safe** — sem estado mutável compartilhado.
- **Performance** — sem reflection, sem `dynamic`, sem `Activator` no caminho crítico.
