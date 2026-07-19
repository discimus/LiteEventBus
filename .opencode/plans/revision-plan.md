# Plano de Revisão — LiteEventBus

## 1. DefaultEventBus.cs — Adicionar validações

**Arquivo:** `src/LiteEventBus/Internal/DefaultEventBus.cs`

Adicionar `ArgumentNullException.ThrowIfNull(serviceProvider)` no construtor e `ArgumentNullException.ThrowIfNull(@event)` no início de `PublishAsync`.

## 2. ServiceCollectionExtensions.cs — Idempotência

**Arquivo:** `src/LiteEventBus/Extensions/ServiceCollectionExtensions.cs`

Adicionar `using System.Linq;` e guard pattern em `AddLiteEventBus` para evitar registros duplicados de `IEventBus`.

## 3. LiteEventBus.csproj — InternalsVisibleTo

Adicionar `InternalsVisibleTo` attribute para `LiteEventBus.Tests`.

## 4. Directory.Build.props — AnalysisMode

Adicionar `<AnalysisMode>All</AnalysisMode>`, `<EnableNETAnalyzers>true</EnableNETAnalyzers>`, `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>`.

## 5. Testes — Reorganização e Expansão

### 5.1 Novos arquivos

- `TestEvents.cs` — `TestEvent`, `AnotherTestEvent`
- `TestSubscribers.cs` — todos os subscribers auxiliares (existente + 3 novos)

### 5.2 DefaultEventBusTests.cs — 8 novos testes

| # | Teste |
|---|-------|
| 11 | `PublishAsync_NullEvent_ThrowsArgumentNullException` |
| 12 | `PublishAsync_CancelledToken_ThrowsOperationCanceledException` |
| 13 | `PublishAsync_NoSubscribersForEventType_EmptyEnumeration` |
| 14 | `PublishAsync_SubscriberThrows_ExceptionMessagePreserved` |
| 15 | `PublishAsync_MultiplePublishes_SequentialCallsMaintainOrder` |
| 16 | `PublishAsync_CancellationToken_CancelledDuringHandle_Propagates` |
| 17 | `PublishAsync_SubscriberReturnsCanceledTask_Propagates` |
| 18 | `PublishAsync_ResolvesTransientPerCall` |

### 5.3 ServiceCollectionExtensionsTests.cs — 7 novos testes

| # | Teste |
|---|-------|
| 1 | `AddLiteEventBus_ReturnsSameInstance` |
| 2 | `AddLiteEventBus_MultipleCalls_NoDuplicateRegistration` |
| 3 | `AddLiteEventBus_NullServices_ThrowsArgumentNullException` |
| 4 | `AddSubscriber_ReturnsSameInstance` |
| 5 | `AddSubscriber_NullServices_ThrowsArgumentNullException` |
| 6 | `AddSubscriber_RegistersTransientLifetime` |
| 7 | `AddSubscriber_MultipleSubscribersDifferentEvents_Independent` |

## 6. Ordem de Implementação

1. `DefaultEventBus.cs` — null checks
2. `ServiceCollectionExtensions.cs` — idempotência
3. `LiteEventBus.csproj` — InternalsVisibleTo
4. `Directory.Build.props` — AnalysisMode
5. `TestEvents.cs` — criar
6. `TestSubscribers.cs` — criar
7. `DefaultEventBusTests.cs` — reescrever com 18 testes
8. `ServiceCollectionExtensionsTests.cs` — criar com 7 testes
9. `dotnet build` + corrigir
10. `dotnet test` (25 testes)

## 7. Risco

`AnalysisMode=All` com `TreatWarningsAsErrors=true` pode quebrar o build. Se ocorrer, remover `TreatWarningsAsErrors` ou ajustar `NoWarn`.
