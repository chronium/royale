---
id: SERVER-003
title: Add local in-process client/server mode
track: SERVER
milestone: M4
priority: medium
dependsOn:
- SERVER-001
- SERVER-004
- SERVER-005
createdAt: 2026-07-04T09:22:04.0349270Z
modifiedAt: 2026-07-06T19:28:53.9572140Z
---

Connect client presentation and authoritative simulation through in-memory command and snapshot queues.

## Completion Notes

Implemented a server-owned in-process session boundary in `Royale.Server`:

* `InProcessServerSession` wraps `HeadlessServerSimulation` and owns per-client input and snapshot queues.
* `InProcessClientConnection` carries the server connection id and authoritative player id for a local client handle.
* Connecting a client allocates a server connection id, creates an authoritative player, creates queues, and enqueues an initial recipient-specific `ServerSnapshot`.
* Valid `PlayerInputCommand` values can be queued per client. Invalid commands are rejected before queueing and are not acknowledged.
* Each in-process session step drains valid queued commands, updates the authoritative player's `LastProcessedInputSequence` for acknowledgement only, advances the server simulation by one tick, and enqueues recipient-specific snapshots.
* Snapshot dequeue/drain APIs are per client. Unknown, disconnected, or disposed handles fail explicitly.
* Disconnect removes the authoritative player and prevents further use of the handle. Disposing the session disposes the wrapped simulation.

Out of scope and still future work: SDL client launch wiring, real UDP networking, serialization, handshake/session protocol, movement processing from commands, combat processing from commands, client prediction, reconciliation, interpolation, and sequence wraparound ordering.