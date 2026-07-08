# Repository Instructions

## Core Rule: Ask Before Assuming

When requirements, behavior, platform support, architecture, or implementation details are unclear, ask the project owner before proceeding.

Do not invent gameplay rules, protocol behavior, file formats, rendering architecture, physics behavior, or platform policy from preference alone. If a decision would become part of the project contract, ask first and record the answer in the relevant task or wiki page.

## Always-On Project Contract

This is an experimental cross-platform, server-authoritative battle royale built from the ground up in .NET.

The goal is a small complete multiplayer game loop, not a general-purpose engine. Prefer concrete, inspectable systems that serve the MVP over speculative abstractions.

All feature work must be driven from the PM board through the PM MCP tool:

- Choose an existing PM task before implementation.
- Move the task to `doing` before changing code.
- Work only on the selected task unless the user explicitly expands the scope.
- Move the task to `done` only after implementation, validation, and documentation are complete.
- If work is blocked, leave the task in `doing` and document the blocker.

The `.pm/` directory is PM storage, not a hand-edited project area. Direct `.pm/` reads are allowed for inspection; direct `.pm/` writes are forbidden. Use PM MCP tools for PM task, metadata, state, milestone, priority, wiki, and project metadata changes.

At the start of implementation work, check the git worktree. If it is dirty, notify the user before changing files. If existing changes are obvious, coherent, and safe to preserve, commit them before starting new work; if they are complex, mixed, surprising, or not obviously safe, stop and ask how to proceed. Never overwrite, revert, or discard existing work unless explicitly instructed.

Keep commits focused on completed PM tasks. Prefix task commits with the PM task ID in square brackets, for example `[COMBAT-001] Add default rifle definition`.

Preserve server authority:

- The server owns authoritative simulation, movement validation, combat, health, ammunition, safe-zone state, match phases, eliminations, winners, and reset.
- The client owns windowing, input devices, rendering, audio or visual feedback, local prediction, interpolation, reconciliation display, and development UI.
- Shared simulation code may exist so server and client prediction use the same rules, but sharing code must not weaken server authority.
- Rendering, SDL windowing, SDL GPU, ImGui, and client UI must not become server dependencies.
- Client input represents intent, not authoritative state.

The PM wiki is a source of truth. Update it when behavior, architecture, setup, protocols, data formats, workflow, or constraints change.

Before marking a task complete, run relevant validation or clearly explain why validation could not be run. If the change needs human validation for rendering, game feel, platform behavior, audio/visual feedback, or UI, call that out explicitly.

## Skill Routing

Use repo skills for detailed task workflows:

- `royale-pm-workflow` — PM board work, task selection, dependencies, milestones, priorities, task notes, wiki edits, or `.pm/` protection.
- `royale-architecture-boundaries` — architecture changes, project layout, dependency direction, authority ownership, server/client/shared boundaries, or source-of-truth architecture docs.
- `royale-build-validation` — restore/build/test commands, CI, packaging, shadercross, .NET SDK, native package validation, or validation command selection.
- `royale-client-rendering-native` — SDL, SDL GPU, ImGui, shader outputs, rendering/UI, debug overlays, native dependency layout, or C# native binding concerns.
- `royale-networking-protocol` — UDP transport, snapshots, protocol messages, identity, sequencing, acknowledgements, versioning, compatibility, or in-process transport behavior.
- `royale-simulation-gameplay` — fixed tick simulation, movement, combat, weapons, safe zone, match phases, eliminations, prediction, reconciliation, or gameplay tests.
- `royale-source-control-implementation` — dirty worktree handling, commits, implementation discipline, generated artifact decisions, documentation completion, and final validation summaries.
- `royale-review` — reviewing diffs, PR-style changes, regressions, authority leaks, missing tests, missing wiki updates, or unsafe native/protocol changes.

When multiple skills match, load the smallest set that covers the current task. For feature work, `royale-pm-workflow` usually comes first.
