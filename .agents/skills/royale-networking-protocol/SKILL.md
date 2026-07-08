---
name: royale-networking-protocol
description: Royale networking and protocol discipline. Use for UDP transport, in-process transport, test/simulated-loss transport, snapshots, input commands, protocol messages, sequencing, ACKs, identity, versioning, or compatibility.
---

# Royale Networking and Protocol Discipline

Use this skill for networking, protocol messages, transport behavior, snapshots, input command flow, sequencing, acknowledgement data, versioning, compatibility, and in-process transport boundaries.

## Network ownership

Keep networking boundaries explicit.

- Clients send input commands and connection messages.
- Servers send authoritative snapshots and events.
- Client input represents intent, not authoritative state.
- Do not let the client call arbitrary server gameplay methods directly in in-process mode.
- Protocol-incompatible clients and servers should fail clearly.

## Transport discipline

Real UDP transport, in-process transport, test transport, and simulated-loss transport should preserve the same message flow.

In-process transport is for development/testing convenience, not a gameplay authority shortcut.

When adding or changing a transport:

- Preserve client input command flow.
- Preserve server authoritative snapshot/event flow.
- Preserve protocol validation and identity handling.
- Preserve sequencing/acknowledgement behavior where the real transport would require it.
- Keep simulated-loss behavior useful for reproducing network issues.

## Protocol messages

Protocol messages should include versioning and enough identity, sequencing, and acknowledgement data to debug behavior.

Favor simple full snapshots early. Optimize only after behavior is correct and measured.

When changing protocol messages:

- Ask before defining new gameplay or compatibility contracts if unclear.
- Update protocol serialization tests.
- Update version handling tests.
- Update wiki documentation for protocol messages and compatibility rules.
- Make incompatible client/server combinations fail clearly.
- Avoid hiding breaking changes behind permissive parsing.

## Snapshot and input expectations

For early MVP behavior:

- Clients send input commands.
- Server validates movement and gameplay.
- Server emits authoritative snapshots and events.
- Clients display interpolation, prediction, and reconciliation corrections.
- Snapshot buffering and correction metrics should be diagnosable.

Protocol tests should cover:

- Serialization/deserialization.
- Version handling.
- Input buffering.
- Sequence comparisons.
- Acknowledgement behavior.
- Invalid packet behavior.
- Compatibility failure modes.

## Debuggability

Network diagnostics should make these visible where practical:

- Client and server ticks.
- Snapshot buffering.
- Prediction corrections.
- Input queue depth.
- Packet counts.
- Packet loss.
- Latency.
- Jitter.
- Invalid packets.

## Workflow

Before implementation:

- Use `royale-pm-workflow` to confirm the selected task.
- Use `royale-architecture-boundaries` if authority or dependency boundaries are touched.
- Inspect protocol tests and wiki pages before changing protocol contracts.

While implementing:

- Keep message flow consistent across real, in-process, test, and simulated-loss transports.
- Do not directly call server gameplay methods from the client path.
- Do not move authoritative state into client code.
- Favor clear, inspectable packet formats and error paths over premature bandwidth optimization.

After implementation:

- Add/update protocol tests.
- Run relevant build/test validation through `royale-build-validation`.
- Update wiki documentation for protocol, compatibility rules, message flow, diagnostics, or transport behavior changes.
