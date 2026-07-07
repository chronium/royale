---
id: TEST-003
title: Add tick-based script execution
track: TEST
milestone: M4
priority: medium
dependsOn:
- TEST-001
- TEST-002
createdAt: 2026-07-05T15:17:24.0585610Z
modifiedAt: 2026-07-06T19:29:49.3728540Z
---

Drive scenarios using simulation ticks rather than wall-clock sleeps and support bounded waits that remain fast in headless execution.

## Implementation Notes

- Added `scenario.clock.waitTicks(count)` for deterministic server tick advancement with a per-call cap of `10000` ticks.
- Added `scenario.clock.waitUntil(maxTicks, predicate)` to evaluate a script predicate at the current tick, then step up to `maxTicks` server ticks until it returns boolean `true`.
- Added `scenario.clock.maxWaitTicks` so scripts can inspect the fixed per-call cap.
- `scenario.server.step(count)` remains the lower-level manual stepping API.
- Predicate calls are synchronous and use Wattle `ScriptExecutionContext`; no wall-clock sleeps, timers, async scheduling, or coroutine machinery were introduced.

## Validation

- `dotnet build Royale.slnx -m:1 --no-restore` passed.
- `dotnet test Royale.slnx -m:1 --no-restore` passed.
- `pm validate_project` passed.

No human validation is required; this is headless test-host behavior covered by automated tests.
