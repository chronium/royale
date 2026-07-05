---
id: PLATFORM-004
title: Add structured logging
track: PLATFORM
milestone: M1
createdAt: 2026-07-04T09:21:26.9337420Z
modifiedAt: 2026-07-04T09:22:36.7140520Z
---

Add timestamped, leveled, subsystem-aware logging suitable for both clients and headless servers.

## Completion Notes

Implemented shared logging in `src/Royale.Diagnostics` using `Microsoft.Extensions.Logging` with ZLogger console output. `RoyaleLogging.CreateConsoleLoggerFactory(LogLevel minimumLevel)` centralizes the stdout sink and readable line format: UTC timestamp, short level, category/subsystem, and message.

Added `tests/Royale.Diagnostics.Tests` coverage for factory creation/disposal, minimum-level filtering, emitted line shape, and one-line-per-entry behavior.

Referenced `Royale.Diagnostics` from the client and server. The server startup message now uses an `ILogger` and ZLogger extension methods instead of `Console.WriteLine`. The client logs startup beginning, SDL video initialization, window creation, GPU device creation, shutdown, and fatal startup errors. No per-frame logging was added.

Documented the logging policy in the `diagnostics` wiki page and linked it from the architecture index. No runtime log-level configuration, JSON-lines logging, file logging, or SDL log callback bridge was added for this task.

Validation run:

```text
dotnet restore Royale.slnx -p:CI_DONT_TARGET_ANDROID=1
dotnet build Royale.slnx -m:1 --no-restore
dotnet test Royale.slnx -m:1 --no-restore
```

All validation commands passed. Build and test output include the existing third-party ImGui binding warning `NU1510` for `System.Runtime.CompilerServices.Unsafe`.