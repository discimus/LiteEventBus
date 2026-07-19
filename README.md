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

## API

### `IEvent`

Interface marcadora para todos os eventos.

### `IEventSubscriber<TEvent>`

Contrato para subscribers. Deve ser implementado por cada assinante de evento.

### `IEventBus`

Contrato para publicação de eventos.

### `IServiceCollection.AddLiteEventBus()`

Registra os serviços de infraestrutura do LiteEventBus no container DI.

### `IServiceCollection.AddSubscriber<TEvent, TSubscriber>()`

Registra um subscriber explicitamente no container DI.

## Comportamento

- Subscribers são executados **sequencialmente** na ordem de registro.
- Se um subscriber lançar uma exceção, a publicação é interrompida imediatamente e a exceção é propagada.
- Subscribers são resolvidos via DI como **transient** a cada chamada de `PublishAsync`.
- O `EventBus` é registrado como **singleton**.
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
