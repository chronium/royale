---
title: Networking Architecture
createdAt: 2026-07-05T16:10:17.3761740Z
modifiedAt: 2026-07-10T05:59:59.3562400Z
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

`SimulatedNetworkTransport` is a game-owned `INetworkTransport` wrapper for deterministic bad-network testing. It sits below handshake, input, snapshot, prediction, and reconciliation code and above any concrete inner transport, including LiteNetLib, in-process, or test transports. Lifecycle calls (`Start`, `Connect`, `Disconnect`, disposal) are delegated to the inner transport. Connected, disconnected, socket error, and latency update events pass through immediately. Only outbound `Send` payloads and inbound `PacketReceived` events are copied and optionally delayed, jittered, dropped, duplicated, or reordered according to `SimulatedNetworkConditions`.

The default simulated condition is `SimulatedNetworkConditions.None`, which applies no impairment. Non-default conditions support fixed latency, symmetric jitter around that latency, loss probability, duplicate probability, reorder probability, and an optional random seed for deterministic tests. The wrapper uses `TimeProvider`, so tests can advance simulated time without sleeping.

`SimulatedNetworkTransport.CurrentConditions` exposes the active immutable condition object, and `SetConditions` replaces it live. Replacement resets the random sequence when a seed is supplied. Drop, duplication, reordering, jitter, and due-time decisions are captured when a packet is queued, so already queued packets keep their original decision and due time after a replacement.

The WattleScript UDP scenario runtime wraps every scripted client transport and leaves the server transport unwrapped. Client-to-server packets are impaired on the client's send path, and server-to-client packets are impaired on the client's receive path, so each direction is impaired exactly once. All scripted UDP client wrappers share a manual clock advanced by one fixed simulation step per scenario tick. `scenario.network.set`, `current`, and `clear` provide deterministic per-player controls while the player remains connected. CLI flags, production runtime controls, debug UI toggles, and broader observability actions remain deferred.

### Optional Transport Diagnostics

Transports may additionally implement the game-owned `INetworkTransportDiagnostics` interface. `TryGetPeerStatistics(NetworkPeerId, out NetworkPeerStatistics)` returns an immutable projection containing one-way latency, RTT, MTU, time since the last packet, sent/received packet and byte totals, and packet-loss count/percentage. Unknown or disconnected peers return no statistics. Higher layers remain valid when the optional interface is absent.

`LiteNetLibNetworkTransport` enables LiteNetLib peer statistics and maps them into `NetworkPeerStatistics`; no LiteNetLib type crosses the `Royale.Network` boundary. `SimulatedNetworkTransport` forwards the optional query when its inner transport supports it. `NetworkClientRuntime` samples and caches the latest successful projection so UI can retain last-known transport totals after disconnect.

The client runtime separately owns application-level counters for successful input sends, all packets received from the server peer, received/valid/invalid snapshot packets, network errors, disconnect reason, and latency samples. Latency jitter is the exponentially smoothed absolute difference between consecutive one-way latency samples using a `1/16` update factor. These counters diagnose protocol/application handling and are intentionally distinct from transport-level UDP totals.

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

`Royale.Network` sends `ProtocolMessageType.ClientInput` packets with the accepted nonzero session id using `NetworkDelivery.Sequenced` on input channel `2`. This keeps input unreliable while preserving the LiteNetLib channel; plain `Unreliable` delivery is not used for client input because LiteNetLib delivers those packets without the requested channel. Each client input packet contains the newest command plus up to three previous commands, serialized newest-first, so normal packet loss can be tolerated without reliable retransmission. `ServerInputReceiver` consumes input packets only from accepted peers with the expected session id and channel. It discards stale or duplicate input commands using monotonic `Sequence` ordering per peer, leaves sequence wraparound handling deferred, and forwards accepted commands to the server callback in ascending sequence order so the in-process server can consume queued commands in simulation-tick order without reordering bursts.

`ServerSnapshotPayloadSerializer` serializes full `ServerSnapshot` payloads with little-endian primitive fields. Nullable `uint` and `ulong` snapshot fields use explicit one-byte presence markers: `0` for absent and `1` for present, followed by the value only when present. Weapon ids are UTF-8 strings with one-byte length prefixes. Snapshot payloads are bounded by `ProtocolConstants.MaxSnapshotPlayers = 128` and `ProtocolConstants.MaxSnapshotWeaponIdLength = 64`.

Snapshot deserialization rejects malformed payloads, oversized player counts, oversized weapon ids, invalid enum values, invalid nullable or boolean markers, truncated data, and trailing bytes.

Event serialization, client disconnect messages, server disconnect messages, snapshot delta compression, and sophisticated acknowledgement policy remain deferred.

`BR-001` keeps protocol v1 unchanged while defining the complete stable one-byte match-phase mapping in snapshot match state:

```text
0 WaitingForPlayers
1 Playing
2 Finished
3 Countdown
4 Resetting
```

Values `1` and `2` preserve the former `InProgress` and `Completed` wire values. Snapshot deserialization accepts only these five defined values, and Wattle scenario snapshot wrappers expose the corresponding names shown above.

## Replication

The replication layer converts authoritative simulation state into network snapshots and applies snapshots to client-side representations.

Early snapshots are full snapshots and intentionally redundant. Optimization should come after protocol behavior is correct and observable.

`ServerSnapshotSender` is the reusable server-side snapshot sending layer. It accepts an `INetworkTransport`, accepted peer/session metadata, and a per-peer `ServerSnapshot` provider. It frames packets as `ProtocolMessageType.ServerSnapshot` with the accepted nonzero `SessionId`, uses protocol packet sequence numbers, and sends via `NetworkDelivery.Sequenced` on snapshot channel `1`.

The sender emits snapshots only when `serverTick % 3 == 0`, giving a 20 Hz snapshot cadence from a 60 Hz simulation. In the executable server runtime, the snapshot provider drains the recipient's authoritative session queue and sends the latest available recipient-specific snapshot so acknowledgements and local-player identity remain server-owned.

The executable client decodes `ServerSnapshot` packets into `ClientNetworkState.LatestSnapshot`, which remains authoritative-only. Connect-mode presentation builds a separate presentation snapshot and does not mutate `LatestSnapshot`.

`NetworkClientRuntime` owns local movement prediction and reconciliation for accepted UDP clients. After `ServerAccept`, it attempts to load client-side collision for the accepted `MapId` through an injectable map loader, defaulting to `MapCatalog.LoadById`. If that map or collision world cannot be created, prediction is disabled and presentation falls back to authoritative snapshots.

Prediction is seeded from the first authoritative snapshot that contains the local player. Each successful fixed input send is stored in a bounded pending-input buffer and, once seeded, is applied immediately through `PlayerMovementIntent.ToWorldMovement` and `KinematicCharacterController.Step` against `MapStaticCollisionWorld` using `SimulationSettings.FixedDeltaSeconds`. The predicted local player updates position, velocity, yaw, and pitch for camera and debug-capsule presentation.

Incoming authoritative local-player snapshots drop pending inputs acknowledged by `AcknowledgedInputSequence`, seed prediction from the authoritative local player state, and replay the remaining unacknowledged commands in sequence order through the same shared movement controller. Replayed commands are local-only; reconciliation does not resend input packets and does not mutate `ClientNetworkState.LatestSnapshot`. Dead authoritative local-player snapshots resync the predicted player to the authoritative dead state and skip movement replay.

`NetworkClientRuntime` exposes lightweight reconciliation diagnostics: pending input count, last correction distance, last replayed input count, and total reconciliation count.

The SDL presentation path applies local-only correction smoothing to the predicted local player before rendering the gameplay camera and debug presentation snapshot. Smoothing preserves the previous displayed position across small reconciliation corrections and decays the offset over presentation frames. It does not change predicted simulation state, authoritative snapshots, network payloads, or server-owned gameplay state.

Remote-player presentation uses `RemoteSnapshotInterpolator`, a client-side bounded snapshot history populated from received authoritative snapshots. The default interpolation delay is `6` server ticks, approximately `100 ms` at the 60 Hz simulation rate and the current 20 Hz snapshot cadence. Each render frame advances the interpolation presentation clock from render `deltaSeconds`, selects bracketing snapshots around the delayed target server tick, and interpolates remote-player position, velocity, yaw, and pitch. Yaw interpolation is shortest-angle and wrap-aware; pitch interpolation is linear.

The local player is never remote-interpolated. Presentation first builds from the latest authoritative snapshot, applies interpolated remote transforms where valid bracketing samples exist, and then substitutes the smoothed predicted local player when available. Non-transform gameplay state such as health, alive state, weapon state, match state, and safe-zone state remains latest-authoritative in the presentation snapshot.

Fallback behavior is intentionally conservative. With fewer than two usable snapshots, the client renders latest authoritative remote state. If a remote player is missing from either bracketing sample, the presentation transform falls back to the nearest buffered authoritative sample for that player. If the interpolation target runs beyond the buffered range, the nearest buffered transform is held; this task does not extrapolate remote players.

`NetworkClientRuntime` exposes lightweight remote interpolation diagnostics: buffered snapshot count, interpolation delay ticks, last interpolation target tick, and whether the last render used interpolation or fallback.

Production bad-network controls, event replication, delta compression, and broader cosmetic smoothing remain outside this task unless already supported by existing presentation code.

### Processed Input Metadata Replication

Each serialized `PlayerSnapshotState` now ends with nullable `LastProcessedInputSequence` and `LastProcessedInputClientTick` values. Both use the existing one-byte presence marker followed by a little-endian `uint` when present. This carries processing metadata only; bot input commands are never sent over UDP.

The v1 protocol version remains unchanged, so this snapshot layout change requires lockstep client/server deployment. Older builds cannot parse the additional per-player fields.

### Participant Kind Replication

Every serialized player snapshot carries a one-byte `ServerSnapshotPlayerKind` immediately after `PlayerId`: `Human = 0`, `Bot = 1`. Serialization rejects undefined enum values; deserialization rejects unknown wire values and truncated payloads. The maximum snapshot payload budget includes one additional byte per possible player.

Participant kind is preserved through decoded client state, local prediction copies, presentation snapshots, and remote interpolation. Rendering behavior is unchanged.

This snapshot layout change deliberately keeps protocol version 1 unchanged. Builds from before and after `BOT-001` are wire-incompatible despite advertising the same protocol version: there is no legacy parsing path for snapshots without participant kind. Client and server builds must therefore be deployed in lockstep.

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

Each `InProcessServerSession.Step` consumes at most one queued valid command per connected client. The oldest queued command for that client is applied to that server simulation tick, and any remaining commands stay queued for later ticks. This preserves short press/release bursts without making the client authoritative. `HeadlessServerSimulation.Step(...)` still receives only intent commands for the current authoritative tick, and the latest processed command sequence becomes the recipient's top-level snapshot acknowledgement. The server also records the processed command's `ClientTick` for debug state and structured observability logs.

The snapshot cadence is unchanged: `ServerSnapshotSender` still emits snapshots only when `serverTick % 3 == 0`, giving a 20 Hz snapshot cadence from a 60 Hz simulation. Server-side input hold/grace is intentionally deferred; queued commands are preserved in order rather than artificially extended.

The in-process session remains authoritative for local/synthetic clients: queued command intent drives server-owned movement, look, rifle firing, ammunition, hitscan player damage, death state, and living-player count. `ServerSnapshotSender` can bridge accepted handshake peers to these queued snapshots in tests. The SDL client still does not reference `Royale.Server`. Winner selection, combat events, respawn, reload, and match reset remain deferred.

### Internal Bot Commands

`BOT-002` extends the authoritative in-process boundary without adding a transport path. `InProcessServerSession.TrySubmitBotInput` and `NetworkServerRuntime.TrySubmitBotInput` accept server-internal `BotInputIntent`; they do not serialize inputs, create fake peers, or bypass gameplay. The session validates intent, assigns sequence and authoritative decision-tick metadata, and holds at most one command per bot for the upcoming step.

During `Step`, pending bots are consumed in ascending player-ID order and joined with the human commands dequeued for that tick. The resulting single player-ID command map enters `HeadlessServerSimulation.Step`. Immediate next-step consumption is temporary; `BOT-014` will introduce scheduled latency delay without changing the simulation input contract.