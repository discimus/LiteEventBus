# Installation

## Package

LiteEventBus is distributed as a single NuGet package with zero external dependencies beyond the .NET DI abstractions.

```shell
dotnet add package LiteEventBus
```

or via the Package Manager Console:

```powershell
Install-Package LiteEventBus
```

## Target Framework

- .NET 8.0+

## Dependencies

| Package | Version | Scope |
|---------|---------|-------|
| `Microsoft.Extensions.DependencyInjection.Abstractions` | 8.0.2 | Compile-time only |

The library has no runtime dependencies. The `Abstractions` package is a compile-only dependency — consumers only need the SDK.

For testing and samples, the following packages are used but are **not** required by the library itself:

| Package | Use |
|---------|-----|
| `Microsoft.Extensions.DependencyInjection` | Building a DI container in tests/samples |
| `xunit` | Unit testing |
| `Microsoft.NET.Test.Sdk` | Test runner |

## Package Information

| Field | Value |
|-------|-------|
| Package ID | `LiteEventBus` |
| License | MIT |
| Authors | Jonata Silva |
| Repository | [github.com/discimus/LiteEventBus](https://github.com/discimus/LiteEventBus) |
| Tags | `pubsub`, `event-bus`, `events`, `messaging`, `di` |

## Versioning

LiteEventBus follows [Semantic Versioning 2.0.0](https://semver.org/). The current version is **0.3.2**.

Breaking changes are communicated via minor version bumps while the library is pre-1.0.

## CI / Build Requirements

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Build: `dotnet build --nologo`
- Test: `dotnet test --nologo`
