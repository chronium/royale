---
id: TEST-009
title: Run scenarios against real UDP transport
track: TEST
milestone: M5
priority: medium
dependsOn:
- TEST-007
- NET-003
- NET-004
- NET-005
- NET-011
createdAt: 2026-07-05T15:17:25.5252770Z
modifiedAt: 2026-07-07T16:45:35.6335800Z
---

Allow the same scenario system to launch or connect to a real dedicated server and exercise serialized traffic over UDP.

## Implementation Notes

Implemented a test-only scenario runtime abstraction in `tests/Royale.Gameplay.Tests` with two modes:

- `scenario.server.start(mapId)` keeps the existing in-process `InProcessServerSession` behavior.
- `scenario.server.startUdp(mapId)` starts an in-process `NetworkServerRuntime` on an ephemeral loopback UDP port with `LiteNetLibNetworkTransport` and connects scripted UDP clients using their own port-0 transports.

UDP scripted clients use `NetworkHandshakeClient`, `ClientInputSender`, protocol framing, and `ServerSnapshotPayloadSerializer` directly from `Royale.Network` and `Royale.Protocol`. The gameplay test project now references `Royale.Network` directly and still does not reference `Royale.Client`.

`scenario.players.connect()` in UDP mode blocks until handshake acceptance and the first decoded snapshot with a bounded timeout. Tick waits remain script-facing tick waits, but the UDP runtime performs bounded internal socket polling/yields so loopback packets can be delivered by the OS socket path.

Added file-backed UDP scenarios:

- `udp-two-clients-see-each-other.wattle`
- `udp-input-acknowledgement.wattle`

Added C# host coverage for invalid and duplicate `startUdp`, nonzero UDP connection/player ids, and UDP disconnect/stop lifecycle behavior.

## Current Limits

This does not launch an external `Royale.Server` process. UDP mode is loopback-only inside the gameplay test process and does not add adverse-network controls, prediction, reconciliation, or interpolation validation.

## Validation

- `dotnet test tests/Royale.Gameplay.Tests/Royale.Gameplay.Tests.csproj -m:1 --no-restore` passed: 89 tests.
- Verified copied scenario files exist under `tests/Royale.Gameplay.Tests/bin/Debug/net10.0/Scenarios`, including both UDP `.wattle` files.
- `dotnet build Royale.slnx -m:1 --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test Royale.slnx -m:1 --no-restore` passed all test projects.