---
title: Diagnostics
createdAt: 2026-07-05T19:44:39.3163150Z
modifiedAt: 2026-07-05T19:44:39.3163150Z
---

## Logging

Royale uses `Microsoft.Extensions.Logging` with ZLogger as the concrete logging implementation for M1 client and server processes.

The shared logging policy lives in `src/Royale.Diagnostics`. Client and server entry points create their console logger factories through `RoyaleLogging.CreateConsoleLoggerFactory(LogLevel minimumLevel)` so both processes use the same formatting and filtering behavior.

The initial sink is stdout through ZLogger console logging. File logging, JSON-lines output, external log shipping, and runtime log-level configuration are intentionally out of scope until launch arguments or server configuration require them.

Expected readable console shape:

```text
2026-07-05 16:45:12.345 [INF] Royale.Server: Royale server skeleton ready. Protocol 1, map default, tick 60 Hz, headless True.
```

Each line includes:

* UTC timestamp
* three-character log level
* logger category, used as the subsystem
* rendered message

Application code should use ZLogger extension methods such as `ZLogInformation`, `ZLogWarning`, and `ZLogCritical` at call sites. Do not pass interpolated strings to normal `LogInformation` methods. Avoid per-frame logging; high-frequency diagnostics should go through debug overlays or sampled metrics until a runtime logging policy exists.

## Current Lifecycle Logs

The server currently logs startup skeleton details through the shared logger factory.

The client currently logs startup beginning, SDL video initialization, SDL window creation, SDL GPU device creation, shutdown, and fatal startup errors.