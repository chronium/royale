---
title: Networking Architecture
createdAt: 2026-07-05T16:10:17.3761740Z
modifiedAt: 2026-07-05T16:10:17.3761740Z
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