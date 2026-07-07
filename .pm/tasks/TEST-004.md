---
id: TEST-004
title: Add assertions and bounded eventual checks
track: TEST
milestone: M4
priority: medium
dependsOn:
- TEST-002
- TEST-003
createdAt: 2026-07-05T15:17:24.3036560Z
modifiedAt: 2026-07-06T19:29:52.8271980Z
---

Provide equality, tolerance, state, event, and eventually-with-timeout assertions with useful failure diagnostics.

## Implementation Notes

Added Wattle scenario assertion helpers for numeric tolerance (`scenario.assert.near`), named immediate state checks (`scenario.assert.state`), host-event existence checks (`scenario.assert.event`), and bounded eventual assertions (`scenario.assert.eventually`). Eventual assertions use deterministic in-process server tick advancement and throw on timeout after exactly the requested tick budget.

Expanded snapshot wrappers to expose sorted player snapshots, per-player lookup, match state, safe-zone state, player position/velocity/look/health/alive/weapon data, and nested read-only value wrappers.

Added `scenario.events` for test-host event history only. Recorded events cover server lifecycle and stepping, scripted player connection/disconnection, input acceptance/rejection, and clock wait satisfaction or timeout. These are not authoritative gameplay or network protocol events.

Updated the `automated-gameplay-testing` wiki page with the new scenario API contract and removed eventual assertions from the not-yet-exposed boundary.

## Validation

- `dotnet build Royale.slnx -m:1 --no-restore` passed with one existing `NU1510` warning from the ImGui binding project.
- `dotnet test tests/Royale.Gameplay.Tests/Royale.Gameplay.Tests.csproj -m:1 --no-restore` passed: 76 tests.
- `dotnet test Royale.slnx -m:1 --no-restore` passed across all test projects.
- `pm validate_project` passed.
