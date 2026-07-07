---
title: Networking Architecture
createdAt: 2026-07-05T16:10:17.3761740Z
modifiedAt: 2026-07-07T04:11:30.1432120Z
---

## Networking Layers

Networking should be divided into several conceptual layers.

## Transport

The transport moves packets between endpoints.

It should expose a small interface and hide the chosen UDP implementation from game code.

```csharp
public interface INetworkTransport : IDisposable
{
    void Send(NetworkEndpoint endpoint, ReadOnlySpan<byte> packet);
    void Poll(INetworkEventHandler handler);
}
```

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

Real transport behavior is still deferred. At this stage, `--connect` is parsed and logged by the client so later networking tasks can attach transport behavior without changing the launch contract.

## Protocol

The protocol layer serializes and deserializes messages.

Initial message types may include:

```text
ClientHello
ServerAccept
ServerReject
ClientInput
ServerSnapshot
ServerEvent
ClientDisconnect
ServerDisconnect
```

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

This mode should preserve the same conceptual boundaries as real networking:

```text
Client
  produces serialized or structured commands

In-process transport
  moves commands between queues

Server
  processes commands and produces snapshots

In-process transport
  returns snapshots to the client
```

The client should not call arbitrary server gameplay methods directly.

Keeping the communication boundary intact makes it easier to introduce UDP later without rewriting the simulation architecture.

An in-process mode also helps with:

* Integration tests
* Automated match tests
* Debugging prediction
* Running multiple simulated clients
* Reproducing packet sequences

SERVER-003 adds the first concrete in-process session boundary in `Royale.Server`. `InProcessServerSession` owns a `HeadlessServerSimulation` and gives each connected local client an `InProcessClientConnection` containing the server connection id and authoritative player id.

Connecting a local client allocates a `ServerConnectionId`, calls `HeadlessServerSimulation.AddPlayer`, creates per-client input and snapshot queues, and enqueues an initial recipient-specific `ServerSnapshot`. That initial snapshot carries the local player id and the current top-level acknowledged input sequence for that player.

Clients submit structured `PlayerInputCommand` intent through the session queue API. Commands are validated with `PlayerInputCommandValidation` before they enter the server queue. Invalid commands are rejected, are not queued, and are not acknowledged. Unknown, disconnected, or disposed client handles fail explicitly.

Each `InProcessServerSession.Step` drains queued valid commands per connected client, updates that player's `LastProcessedInputSequence` for acknowledgement only, advances the authoritative simulation by one tick, and enqueues a recipient-specific snapshot for every connected client. Snapshot dequeue and drain APIs are per client. Disconnecting a local client removes its authoritative player and prevents further use of that handle.

This is not real UDP transport and does not add serialization, handshake, loss simulation, client launch wiring, client prediction, reconciliation, interpolation, movement processing, combat processing, or snapshot send-rate throttling. The SDL client still does not reference `Royale.Server`.