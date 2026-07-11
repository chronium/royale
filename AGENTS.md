# Repository Instructions

## Operating Model

Work autonomously after grounding yourself in the repository. Inspect code, tests, PM data, and wiki pages before asking questions.

Ask the project owner when a decision would create or change a project contract and the answer cannot be discovered locally. Contract decisions include gameplay rules, protocol compatibility, file formats, rendering or physics behavior, platform policy, dependency selection, and authority ownership.

Do not ask about discoverable facts, routine implementation details, or choices already established by nearby code or the wiki. When several implementations satisfy an established contract, choose the smallest one consistent with current patterns.

## Project Contract

Royale is a small cross-platform, server-authoritative battle royale built in .NET 10. The goal is a complete, inspectable multiplayer game loop, not a general-purpose engine.

- The server owns authoritative simulation, movement validation, combat, health, ammunition, safe-zone state, match phases, eliminations, winners, and reset.
- The client owns windowing, input devices, rendering, audiovisual feedback, prediction, interpolation, reconciliation presentation, and development UI.
- Shared simulation code may support client prediction, but sharing code must not weaken server authority.
- Client input expresses intent. It never declares authoritative position, damage, death, pickup ownership, or match results.
- Rendering, SDL windowing, SDL GPU, ImGui, and client UI must not become server dependencies.
- Keep project folders and namespace suffixes aligned. Use explicit file-level `using` directives; do not introduce project-wide global usings.
- Add abstractions only for a concrete game, deployment, testing, or dependency need.

## PM And Wiki

All implementation work must have a PM task managed through PM MCP.

- Planning and review may inspect tasks without changing their state.
- Before editing implementation files, select or create the task and move it to `doing`.
- Stay within the selected task unless the owner explicitly expands scope.
- Move a task to `done` only after implementation, validation, task notes, and required wiki updates are complete.
- Leave blocked work in `doing` and record the blocker.

The PM wiki is a source of truth for behavior, architecture, setup, protocols, formats, workflows, and constraints. Keep it current as part of the feature, not as follow-up work.

Never write under `.pm/` directly. Reads are allowed for inspection; all task, state, metadata, milestone, priority, ordering, project, and wiki mutations must use PM MCP. If PM MCP lacks a required mutation, stop and report the missing capability.

## Git And Completion

Check the worktree before implementation.

- Clean tree: proceed.
- Dirty tree with one obvious coherent change: notify the owner and commit it before new work.
- Dirty tree with mixed, surprising, or ambiguous changes: stop and ask how to proceed.
- Never discard or overwrite existing work without explicit instruction.

Keep commits task-focused and prefix them with the task ID, for example `[COMBAT-001] Add default rifle definition`. Create branches only when they materially reduce risk; do not create one per task by default.

Before completion, run the relevant documented validation or state exactly why it could not run. Update PM notes and the wiki, then commit. Explicitly request owner validation for rendering, UI, platform behavior, audiovisual feedback, camera feel, movement feel, or combat feel.

## Skill Routing

Load the smallest set of repository skills that covers the work:

- `royale-pm-workflow`: task selection/lifecycle, dependencies, priorities, milestones, PM mutations, and wiki operations.
- `royale-source-control-implementation`: dirty-tree handling, implementation discipline, commits, and completion reporting.
- `royale-build-validation`: restore/build/test, launch profiles, OTLP validation, shader builds, packaging, and native validation.
- `royale-architecture-boundaries`: project structure, dependency direction, authority ownership, and MVP scope.
- `royale-client-rendering-native`: SDL3, SDL GPU, ImGui, shaders, client rendering, debug UI, native bindings, and visual validation.
- `royale-networking-protocol`: transports, framing, handshake, input commands, snapshots, sequencing, acknowledgements, and compatibility.
- `royale-simulation-gameplay`: fixed-tick simulation, movement, combat, match rules, prediction, reconciliation, and gameplay tests.
- `royale-review`: review-only passes for defects, regressions, authority leaks, protocol/native risk, and missing tests or documentation.

For implementation, PM workflow and source-control discipline apply even when only a domain skill is loaded. Do not load unrelated domain skills preemptively.
