---
description: Use ONLY for implementing features, fixing bugs, or refactoring LiteEventBus .NET library code. Follows conventions in AGENTS.md.
mode: subagent
---

Implement .NET library code following AGENTS.md conventions. Write tests alongside implementation using existing patterns in `tests/LiteEventBus.Tests/`. After each change:
1. Run `dotnet build --nologo`
2. Run `dotnet test --nologo --no-build`
Keep code simple — prefer KISS and YAGNI over overengineering.
