---
title: Networking Architecture
createdAt: 2026-07-05T16:10:17.3761740Z
modifiedAt: 2026-07-07T17:14:42.9868440Z
---

## Networking Layers

Networking should be divided into several conceptual layers.

## Transport

The transport moves opaque packets between connected peers. It does not define protocol framing, handshake messages, serialization, input commands, snapshots, prediction, reconciliation, or client/server launch wiring.

`Royale.Network` owns the game transport abstraction:

```csharp
public interface INetworkTransport : IDisposable
{
    void Start(int port);
    NetworkPeerId Connect(NetworkEndpoint endpoint);
    void Send(NetworkPeerId peerId, ReadOnlySpan<byte> packet, NetworkDelivery delivery, byte channel = 0);
    void Disconnect(NetworkPeerId peerId);
    void Poll(INetworkEventHandler handler);
}
```

The public transport surface uses game-owned types: `NetworkEndpoint`, `NetworkPeerId`, `NetworkDelivery`, `NetworkDisconnectReason`, and `INetworkEventHandler`. Packet payloads are opaque byte spans or copied byte buffers at this layer.

`LiteNetLibNetworkTransport` is the first real UDP implementation. It owns LiteNetLib's listener and manager, accepts incoming LiteNetLib connection requests at the transport level, maps LiteNetLib delivery and disconnect values into game-owned enums, and supports LiteNetLib channels `0` through `63`. Only `Royale.Network` references LiteNetLib; `Royale.Protocol`, `Royale.Simulation`, client/server gameplay code, and higher layers must not reference LiteNetLib directly.

The same higher-level connection code should work with:

* Real UDP transport
* In-process transport
* Test transport
* Simulated-loss transport

## Connection

The connection layer manages:

* Handshake
* Session identifiers
* Timeouts
* Packet sequencing
* Duplicate detection
* Acknowledgements
* Connection state
* Disconnect reasons

Client and server launch options expose the default network port contract. Both sides default to port `7777`. The client accepts `--connect <host>` and `--port <port>` to connect to a remote endpoint, and the server accepts `--port <port>` to select its listen port.

`Royale.Network` provides reusable handshake handlers on top of `INetworkTransport` and the protocol frame:

* `NetworkHandshakeClient` starts in `Pending`, sends `ClientHello` with `SessionId = 0`, and transitions to `Accepted`, `Rejected`, or `Disconnected`.
* `NetworkHandshakeServer` consumes pre-session `ClientHello`, validates the framed protocol major version plus build/content compatibility fields, allocates a nonzero session id, and replies with `ServerAccept`.
* `ServerAccept` carries the assigned `SessionId`, `ConnectionId`, `PlayerId`, `ServerTick`, and `MapId` bootstrap fields.
* `ServerReject` is sent with `SessionId = 0` for malformed frames where a response is possible, unexpected pre-session message types, unsupported protocol major versions, incompatible build/content values, and accept callback failures.
* Handshake packets use reliable ordered delivery on channel `0`.

`Royale.Server` owns `NetworkServerRuntime`, which starts from the dedicated server process, polls `LiteNetLibNetworkTransport`, runs `NetworkHandshakeServer`, maps accepted peers to authoritative in-process session connections, queues accepted `ClientInput` commands for the matching player, steps the authoritative session once per fixed tick, sends due snapshots, and removes the peer/player mapping on disconnect.

`Royale.Client` owns `NetworkClientRuntime` for `--connect`. It starts a client UDP transport on an ephemeral local port, calls `Connect`, and creates `NetworkHandshakeClient` only after the transport reports the server peer as connected so `ClientHello` is sent on an established peer. After `ServerAccept`, the runtime creates `ClientInputSender`; each fixed client tick sends one input command with normalized move intent, jump/fire buttons, immediate local yaw/pitch, and a monotonic client command sequence.

This path preserves the dependency boundary: the SDL client references `Royale.Network` but not `Royale.Server`; the dedicated server references `Royale.Network` but not SDL, GPU, ImGui, client rendering, or client UI.

## Protocol

The protocol layer serializes and deserializes messages.

`Royale.Protocol` defines the v1 binary packet frame. It is transport-independent and does not reference `Royale.Network` or LiteNetLib.

Every packet begins with this 29-byte little-endian header:

```text
Offset  Size  Field
0       4     Magic, bytes "ROYL" on the wire (`0x4C594F52` as a little-endian uint32)
4       2     MajorVersion, currently 1
6       2     MinorVersion, currently 0
8       8     SessionId
16      1     MessageType
17      4     Sequence
21      4     AcknowledgedSequence
25      4     AcknowledgementMask
```

Initial message type values are:

```text
1 ClientHello
2 ServerAccept
3 ServerReject
4 ClientInput
5 ServerSnapshot
6 ServerEvent
7 ClientDisconnect
8 ServerDisconnect
```

`SessionId = 0` is reserved for pre-session packets such as `ClientHello` and pre-session `ServerReject`. Accepted-session packets use a nonzero server-assigned `ulong` session id.

`AcknowledgedSequence` is the latest remote packet sequence received. `AcknowledgementMask` bit 0 acknowledges `AcknowledgedSequence - 1`; bit 31 acknowledges `AcknowledgedSequence - 32`. Sequence comparison uses wrapping `uint` arithmetic so acknowledgement coverage remains well-defined across sequence wraparound.

The frame parser validates only wire framing concerns: minimum header length, packet magic, supported major version, and known message type.

Handshake payload serialization exists for `ClientHello`, `ServerAccept`, and `ServerReject`. Handshake strings are UTF-8 with one-byte length prefixes and fixed maximum encoded lengths. Current deterministic development compatibility fields are `BuildId = "dev-build"` and `ContentVersion = "dev-content-1"`. `ServerRejectReason` values are stable wire values.

`ClientInputPayloadSerializer` serializes accepted-session `PlayerInputCommand` batches. A client input payload is a one-byte command count followed by 1 to 4 command records. Each command record is little-endian and contains `Sequence`, `ClientTick`, `Move.X`, `Move.Y`, `YawRadians`, `PitchRadians`, and `Buttons`. Payload deserialization rejects empty batches, oversized batches, invalid movement, invalid pitch, non-finite floats, undefined button bits, truncated payloads, and trailing bytes.

`Royale.Network` sends `ProtocolMessageType.ClientInput` packets with the accepted nonzero session id using `NetworkDelivery.Sequenced` on input channel `2`. This keeps input unreliable while preserving the LiteNetLib channel; plain `Unreliable` delivery is not used for client input because LiteNetLib delivers those packets without the requested channel. Each client input packet contains the newest command plus up to three previous commands, serialized newest-first, so normal packet loss can be tolerated without reliable retransmission. `ServerInputReceiver` consumes input packets only from accepted peers with the expected session id and channel. It discards stale or duplicate input commands using monotonic `Sequence` ordering per peer, leaves sequence wraparound handling deferred, and forwards accepted commands to the server callback in ascending sequence order so the in-process server's latest-command-for-tick behavior stays stable.

`ServerSnapshotPayloadSerializer` serializes full `ServerSnapshot` payloads with little-endian primitive fields. Nullable `uint` and `ulong` snapshot fields use explicit one-byte presence markers: `0` for absent and `1` for present, followed by the value only when present. Weapon ids are UTF-8 strings with one-byte length prefixes. Snapshot payloads are bounded by `ProtocolConstants.MaxSnapshotPlayers = 128` and `ProtocolConstants.MaxSnapshotWeaponIdLength = 64`.

Snapshot deserialization rejects malformed payloads, oversized player counts, oversized weapon ids, invalid enum values, invalid nullable or boolean markers, truncated data, and trailing bytes.

Event serialization, client disconnect messages, server disconnect messages, snapshot delta compression, and sophisticated acknowledgement policy remain deferred.

## Replication

The replication layer converts authoritative simulation state into network snapshots and applies snapshots to client-side representations.

Early snapshots are full snapshots and intentionally redundant. Optimization should come after protocol behavior is correct and observable.

`ServerSnapshotSender` is the reusable server-side snapshot sending layer. It accepts an `INetworkTransport`, accepted peer/session metadata, and a per-peer `ServerSnapshot` provider. It frames packets as `ProtocolMessageType.ServerSnapshot` with the accepted nonzero `SessionId`, uses protocol packet sequence numbers, and sends via `NetworkDelivery.Sequenced` on snapshot channel `1`.

The sender emits snapshots only when `serverTick % 3 == 0`, giving a 20 Hz snapshot cadence from a 60 Hz simulation. In the executable server runtime, the snapshot provider drains the recipient's authoritative session queue and sends the latest available recipient-specific snapshot so acknowledgements and local-player identity remain server-owned.

The executable client decodes `ServerSnapshot` packets into a minimal latest-snapshot state. Connect-mode presentation uses that latest authoritative state directly: local and remote players are rendered as debug capsules in debug render modes, with the local player color distinct from remote players. The connect-mode gameplay camera uses the authoritative local snapshot position plus immediate local look input; before the first local snapshot, it falls back to a stable origin camera.

Smooth interpolation, local prediction, reconciliation/replay display, event replication, delta compression, and bad-network policy remain deferred to their dedicated networking tasks.

## Protocol Versioning

Connection compatibility is checked during handshake.

The v1 frame header carries the protocol major/minor version. `ProtocolPacketFramer` rejects unsupported major versions, and the handshake server maps that failure to `ServerRejectReason.UnsupportedProtocolVersion` when it can reply to the peer.

`ClientHello` also carries deterministic development compatibility fields:

```text
BuildId = "dev-build"
ContentVersion = "dev-content-1"
```

A server rejects mismatched build ids with `IncompatibleBuild` and mismatched content versions with `IncompatibleContent`. Sophisticated backwards compatibility remains out of scope for the first implementation; incompatible clients and servers fail clearly rather than attempting to continue.

## In-Process Development Mode

Before real networking is introduced, the client should be able to communicate with a server simulation through in-memory queues.

This mode preserves the same conceptual boundaries as real networking:

```text
Client
  produces structured commands

In-process transport/session
  queues commands per connected client

Server
  applies commands to authoritative simulation and produces snapshots

In-process transport/session
  returns recipient-specific snapshots to each client
```

The client should not call arbitrary server gameplay methods directly. Keeping the communication boundary intact makes it easier to introduce UDP later without rewriting the simulation architecture.

An in-process mode also helps with:

* Integration tests
* Automated match tests
* Debugging prediction
* Running multiple simulated clients
* Reproducing packet sequences

`InProcessServerSession` owns a `HeadlessServerSimulation` and gives each connected local client an `InProcessClientConnection` containing the server connection id and authoritative player id. Connecting a local client allocates a `ServerConnectionId`, calls `HeadlessServerSimulation.AddPlayer`, creates per-client input and snapshot queues, and enqueues an initial recipient-specific `ServerSnapshot`.

Clients submit structured `PlayerInputCommand` intent through the session queue API. Commands are validated with `PlayerInputCommandValidation` before they enter the server queue. Invalid commands are rejected, are not queued, and are not acknowledged. Unknown, disconnected, or disposed client handles fail explicitly.

Each `InProcessServerSession.Step` drains queued valid commands per connected client, keeps only the latest drained command for each player for that server tick, passes those commands to `HeadlessServerSimulation.Step(...)`, and enqueues a recipient-specific snapshot for every connected client. The latest processed command sequence becomes the recipient's top-level snapshot acknowledgement.

The in-process session remains authoritative for local/synthetic clients: queued command intent drives server-owned movement, look, rifle firing, ammunition, hitscan player damage, death state, and living-player count. `ServerSnapshotSender` can bridge accepted handshake peers to these queued snapshots in tests, but executable client/server startup, client prediction, reconciliation, interpolation, winner selection, combat events, respawn, reload, and match reset remain deferred. The SDL client still does not reference `Royale.Server`.