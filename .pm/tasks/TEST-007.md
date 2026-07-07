---
id: TEST-007
title: Run scenarios against the in-process transport
track: TEST
milestone: M4
priority: medium
dependsOn:
- TEST-003
- TEST-004
- TEST-005
- SERVER-003
- SERVER-005
createdAt: 2026-07-05T15:17:25.0324710Z
modifiedAt: 2026-07-06T19:30:02.1686480Z
---

Execute scripted clients against the authoritative server through in-memory queues while preserving the normal command and snapshot boundaries.

## Notes

- Scenario scripts for this task live as copied `.wattle` test content under `tests/Royale.Gameplay.Tests/Scenarios/` and run from the test output directory by default.
- The script surface remains `ScenarioApi`; scenarios enqueue client input, step the in-process server session, and observe snapshots rather than calling server simulation internals directly.
- `ROYALE_SCENARIO_DIR` may point the runner at an external directory for quick scenario iteration without rebuilding copied test content.
- Added initial in-process scenarios for two clients observing both players, queued input acknowledgement, and invalid input rejection without authoritative movement or acknowledgement.

## Validation

- `dotnet test Royale.slnx -m:1 --no-restore` passed on 2026-07-07.
- Confirmed copied `.wattle` files exist under `tests/Royale.Gameplay.Tests/bin/Debug/net10.0/Scenarios/` after the test build.
- Updated `automated-gameplay-testing` wiki with the file-backed scenario workflow.
