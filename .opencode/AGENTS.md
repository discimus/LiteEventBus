# LiteEventBus — Project Conventions

## Structure
- `src/LiteEventBus.Abstractions/` — Public contracts (`IEvent`, `IEventBus`, `IEventSubscriber<T>`)
- `src/LiteEventBus/` — Implementation (`DefaultEventBus`, `ServiceCollectionExtensions`)
- `tests/LiteEventBus.Tests/` — xUnit tests (25 tests, 4 classes)
- `samples/LiteEventBus.ConsoleSample/` — Usage example

## Code
- Nullable Reference Types enabled
- File Scoped Namespace
- Implicit Usings
- `sealed` when appropriate, `readonly` whenever possible
- `ArgumentNullException.ThrowIfNull` for parameter validation
- `ConfigureAwait(false)` in library internal code
- XML Documentation on all public API
- No reflection, `dynamic`, or `Activator` in the critical path

## Commands
- Build:          `dotnet build --nologo`
- Test:           `dotnet test --nologo`
- Test (no build): `dotnet test --nologo --no-build`
- Run sample:     `dotnet run --project samples/LiteEventBus.ConsoleSample`

## Testing
- Never modify existing tests — create new tests for new features
- Every new public API addition must have matching tests in `ServiceCollectionExtensionsTests.cs` and/or `DefaultEventBusTests.cs`

## Architecture
- `IEventBus` is Singleton
- Subscribers are Transient (resolved via DI per `PublishAsync` call)
- Sequential execution, registration order preserved
- First subscriber exception stops propagation immediately
- No Service Locator in public API
