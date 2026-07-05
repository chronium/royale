---
title: Project Overview
createdAt: 2026-07-04T16:54:42.2894730Z
modifiedAt: 2026-07-05T16:15:30.7132680Z
---

## Introduction

This project is an experimental cross-platform multiplayer battle royale built from the ground up in .NET.

The immediate goal is not to reproduce the scale or feature set of a commercial game such as PUBG. Instead, the project is intended to prove that a small custom technology stack can support the complete lifecycle of a server-authoritative multiplayer match across macOS, Linux, and potentially Windows.

The first meaningful version will be intentionally small:

* A dedicated Linux server
* macOS and Linux clients
* A small gray-box map
* A handful of players
* First-person movement
* One weapon
* Server-authoritative combat
* A shrinking safe zone
* Elimination, spectating, and a winner
* Automatic match reset

Once that complete loop works reliably, the project can grow in scale and complexity.

## High-Level Architecture

The solution is divided into several major components.

## Client

The client is responsible for:

* Window and input handling
* Rendering
* Audio and visual feedback
* Local movement prediction
* Snapshot interpolation
* Server reconciliation
* Player-facing UI
* Development tools

The client does not decide authoritative combat or match results.

## Server

The dedicated server is responsible for:

* Running the authoritative fixed-timestep simulation
* Processing player inputs
* Validating movement and weapon use
* Applying damage
* Managing players and connections
* Controlling match phases
* Updating the safe zone
* Determining eliminations and the winner
* Publishing snapshots to clients

The server runs without SDL window or GPU initialization.

## Shared Simulation

Shared simulation code defines the common game concepts used by both the server and client.

This may include:

* Input command structures
* Character movement rules
* Weapon definitions
* Match state
* Player state
* Timing constants
* Prediction-compatible simulation logic

The server remains authoritative even where simulation code is shared.

## Network Protocol

The protocol defines:

* Connection handshake
* Protocol versioning
* Input command packets
* Snapshot packets
* Sequencing
* Acknowledgements
* Connection and player identifiers

Early versions will favor simplicity over bandwidth efficiency.

Full snapshots and straightforward serialization are acceptable until profiling demonstrates a need for delta compression or more sophisticated encoding.