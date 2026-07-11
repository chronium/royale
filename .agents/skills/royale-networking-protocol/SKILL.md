---
name: royale-networking-protocol
description: Implement or review Royale networking and protocol behavior. Use for LiteNetLib/UDP, transport abstractions, in-process or simulated transport, framing, handshake, sessions, input redundancy, snapshots, sequencing, acknowledgements, prediction transport data, versioning, or compatibility.
---

# Royale Networking And Protocol

## Invariants

- Clients send connection messages and input intent; servers send authoritative snapshots/events.
- Real UDP, in-process, scripted, and impairment transports preserve the same conceptual message flow.
- In-process mode must not call arbitrary gameplay methods from the client path.
- Transport-specific types stay behind game-owned abstractions.
- Full, inspectable snapshots are preferred until profiling justifies compression.

## Protocol Changes

Before changing a wire contract, inspect serializers, bounds, framing, handshake admission, compatibility tests, and the networking wiki.

Ask the owner when compatibility policy is not already recorded. A wire-layout change must explicitly choose and document version behavior, lockstep deployment expectations, malformed-input handling, and old/new client-server failure behavior. Do not silently accept unknown fields/bits or hide breaking changes behind permissive parsing.

Keep IDs, sequence ordering, acknowledgements, channels, delivery mode, and redundancy behavior explicit. Avoid defining reliability or wraparound semantics that the task does not require.

## Prediction Boundary

Client prediction and reconciliation may consume authoritative snapshot metadata, but they do not mutate authoritative snapshots or server state. Remote entities use buffered authoritative presentation. Network impairment controls must remain deterministic in tests when seeded.

## Required Coverage

Test the changed layer:

- byte-level serialization, bounds, malformed payloads, and version handling;
- handshake acceptance/rejection and identity/session ownership;
- sequence, acknowledgement, redundancy, duplicate/stale handling, and channels;
- parity across real/in-process/test transports;
- prediction/reconciliation or interpolation behavior when transport data changes;
- latency/loss/jitter/reordering behavior for impairment changes.

Expose diagnostics for ticks, queues, packets, bytes, loss, latency, jitter, snapshot buffering, and correction behavior where relevant. Update networking/protocol wiki pages in the same task.
