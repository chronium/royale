---
title: Networking Architecture
createdAt: 2026-07-05T16:10:17.3761740Z
modifiedAt: 2026-07-07T09:55:52.7713460Z
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

Client and server launch options already expose the default network port contract. Both sides default to port `7777`. The client accepts `--connect <host>` and `--port <port>` to describe the intended remote endpoint, and the server accepts `--port <port>` to select its listen port.

UDP packet transport exists in `Royale.Network`, but executable launch wiring and higher-level connection behavior are still deferred. At this stage, `--connect` is parsed and logged by the client so later networking tasks can attach handshake, framing, session state, and protocol compatibility behavior without changing the launch contract.

## Protocol

The protocol layer serializes and deserializes messages.

`Royale.Protocol` defines the v1 binary packet frame. It is transport-independent and does not reference `Royale.Network` or LiteNetLib. Payload serialization remains message-specific and is deferred to later networking tasks; NET-002 only defines the fixed packet header plus helpers for writing and reading a header with an opaque payload span.

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

`SessionId = 0` is reserved for pre-session packets such as `ClientHello` and pre-session `ServerReject`. Accepted-session packets use a nonzero server-assigned `ulong` session id. NET-003 will enforce handshake, connection, and session state rules.

`AcknowledgedSequence` is the latest remote packet sequence received. `AcknowledgementMask` bit 0 acknowledges `AcknowledgedSequence - 1`; bit 31 acknowledges `AcknowledgedSequence - 32`. Sequence comparison uses wrapping `uint` arithmetic so acknowledgement coverage remains well-defined across sequence wraparound.

The frame parser validates only wire framing concerns: minimum header length, packet magic, supported major version, and known message type. Handshake compatibility, session rules, transport behavior, input message serialization, snapshot serialization, event serialization, client launch wiring, and server snapshot sending are deferred.

## Replication

The replication layer converts authoritative simulation state into network snapshots and applies snapshots to client-side representations.

Early snapshots can be simple and redundant. Optimization should come after protocol behavior is correct and observable.

## Protocol Versioning

Every connection handshake should include a protocol version.

A client and server with incompatible versions should fail clearly rather than attempting to continue.

For example:

```text
Protocol major version
Protocol minor version
Build identifier
Content or map version
```

The first implementation does not need sophisticated backwards compatibility. It only needs explicit incompatibility detection.

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

SERVER-006 makes this path authoritative for local/synthetic clients: queued command intent now drives server-owned movement, look, rifle firing, ammunition, hitscan player damage, death state, and living-player count. It still does not add real UDP transport, serialization, handshake, loss simulation, client launch wiring, client prediction, reconciliation, interpolation, snapshot send-rate throttling, winner selection, combat events, respawn, reload, or match reset. The SDL client still does not reference `Royale.Server`.