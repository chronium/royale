---
title: Project Goals
createdAt: 2026-07-05T16:14:02.1800850Z
modifiedAt: 2026-07-05T16:14:02.1800850Z
---

## Cross-Platform Support

The client should run natively on:

* macOS
* Linux
* Windows, as a later milestone

The dedicated server should run headlessly on Linux without requiring a graphics environment.

All supported clients should connect to the same server implementation and use the same network protocol.

## Server-Authoritative Multiplayer

The server owns the canonical game state, including:

* Player movement
* Health
* Damage
* Weapon fire cadence
* Ammunition
* Death and elimination
* Safe-zone state
* Match phases
* Winner determination

Clients send input commands rather than directly changing authoritative state.

Local movement prediction and server reconciliation will be used to keep movement responsive, while remote players will be rendered using buffered snapshot interpolation.

## A Focused Custom Engine

The project uses a small custom engine designed specifically for this game.

It is not intended to become a general-purpose engine or compete with Unity, Unreal Engine, or Godot.

New abstractions should be introduced only when the game requires them. The project should avoid speculative systems such as:

* A general-purpose scene editor
* A universal entity-component framework
* A material graph
* A render graph
* A scripting language
* A plugin ecosystem

The game is the product. The engine exists only to support it.

## Incremental Vertical Slices

Development is organized around milestones that produce demonstrable outcomes.

Examples include:

* Opening a window and rendering a cube
* Running Box3D through C# bindings
* Moving a first-person character through a collision map
* Completing an offline combat encounter
* Running an authoritative local simulation
* Connecting macOS and Linux clients to a Linux server
* Completing an entire battle-royale match

Subsystem tasks may belong to different tracks, but milestones combine those tasks into complete vertical slices.