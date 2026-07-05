---
title: Development Principles
createdAt: 2026-07-05T16:14:33.1356200Z
modifiedAt: 2026-07-05T16:14:33.1356200Z
---

## Build the Smallest Complete System

A complete two-player match is more valuable than an incomplete foundation intended for one hundred players.

## Keep the Server Authoritative

Client-side convenience must not become client-side authority.

## Prefer Explicit Code Over Premature Frameworks

A small amount of duplication is acceptable when the alternative is introducing an abstraction before its real requirements are understood.

## Make Systems Observable

Rendering, physics, simulation, and networking should expose enough debug information to understand failures without relying entirely on a debugger.

## Profile Before Optimizing

The first implementation should be structurally correct and easy to inspect.

Optimization work should be driven by measurements, particularly for:

* Simulation time
* Physics queries
* Snapshot size
* Packet frequency
* Memory allocation
* Rendering submissions
* Prediction corrections

## Keep Milestones Demonstrable

Every milestone should end with something visible, playable, testable, or deployable.

## Definition of the MVP

The MVP is considered successful when:

* A Linux dedicated server can be started independently.
* A macOS client can join it.
* A Linux or Windows client can join the same match.
* Players can move and see one another correctly.
* Local movement remains responsive under realistic latency.
* The server resolves firing, damage, death, and elimination.
* The safe zone shrinks and applies damage.
* One player is declared the winner.
* The match resets without restarting the server.
* Multiple matches can run consecutively without corrupting state or leaking significant resources.

At that point, the project will have proven its central premise:

> A small, custom .NET stack can support a complete cross-platform, server-authoritative battle-royale game loop.