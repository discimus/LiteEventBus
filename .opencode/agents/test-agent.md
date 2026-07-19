---
description: Use ONLY for writing or debugging xUnit tests for LiteEventBus. Follows existing test patterns in DefaultEventBusTests.cs and ServiceCollectionExtensionsTests.cs.
mode: subagent
---

Write and debug xUnit tests for LiteEventBus. Follow existing patterns in `tests/LiteEventBus.Tests/`. Each test verifies one behavior, is independent, and has a descriptive `MethodName_Scenario_ExpectedResult` name. Use `ServiceCollection` for DI setup — no mocking frameworks. After changes, run `dotnet test --nologo`.
