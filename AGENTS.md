# Repository Instructions

## Core Rule: Ask Before Assuming

When requirements, behavior, platform support, architecture, or implementation details are unclear, ask the project owner before proceeding.

Do not invent gameplay rules, protocol behavior, file formats, rendering architecture, physics behavior, or platform policy from preference alone. If a decision would become part of the project contract, ask first and record the answer in the relevant task or wiki page.

## PM Board Workflow

All feature work must be driven from the PM board through the PM MCP tool.

- Before starting implementation, choose an existing task from the board with `list_tasks` or inspect a known task with `get_task`.
- Move the task to `doing` with `move_task` before changing code.
- Work only on the selected task unless the user explicitly expands the scope.
- If the task is too vague, ask clarifying questions before implementation.
- Update the task markdown with useful notes, decisions, or acceptance details when they materially affect future work.
- When the implementation, tests, and documentation are complete, move the task to `done` with `move_task`.
- If work is blocked, leave the task in `doing` and document the blocker in the task markdown.

Current PM states are:

- `todo` - Not started
- `doing` - In progress
- `done` - Done

Do not silently work around the board. The board is the source of execution state for agents.

### Choosing the Next Task

Use `get_next_task` when available instead of guessing from raw task lists.

- For implementation work, call `get_next_task` with `readyOnly: true` so blocked tasks are not selected accidentally.
- For planning, sequencing, or diagnosing why a track has no available work, the default `get_next_task` behavior is useful because it may return the best blocked task with its dependency blockers.
- Always inspect the returned task's `dependenciesReady`, `waitingOnDependencies`, priority, milestone, and reason before acting.
- If `readyOnly: true` returns no task for a filtered track, do not bypass dependencies; either choose a ready task from another track or plan the dependency that unblocks the requested track.

### PM MCP Tool Usage

Prefer the most specific PM MCP tool that fits the operation.

- Use `search_tasks` to find related task IDs, dependency references, or prior decisions before changing board structure.
- Use `update_task_metadata` for task title, description, track, milestone, priority, or dependency changes instead of rewriting full task markdown.
- Use `update_task_markdown` only when task notes or acceptance details need markdown changes that metadata tools cannot express.
- Use `move_task` for state changes. Never move task refs manually.
- Use `set_milestone_priority` and task `priority` metadata to guide sequencing rather than relying on manual ordering alone.
- Use `reorder_tasks` only when an explicit within-track order matters after priority and dependency data are already correct.
- Use `bulk_assign_tasks_to_milestone` for milestone reshuffles instead of editing tasks one by one.
- Use `validate_project` after PM structure changes, especially after dependency, priority, milestone, task order, wiki rename, or wiki delete operations.
- Use `get_project`, `list_milestones`, `list_tracks`, and `list_states` when checking configured keys rather than inferring them from file names.

For wiki work:

- Use `list_wiki_pages` or `search_wiki_pages` before creating a page so duplicate source-of-truth pages are not introduced.
- Use `outline_wiki_page` followed by `patch_wiki_page` for targeted edits under an existing heading.
- Use `update_wiki_page_markdown` only for broad rewrites where a targeted patch would be less clear.
- Use `rename_wiki_page` and `remove_wiki_page` for wiki restructuring. Never rename or delete wiki files manually.
- Use `create_wiki_page` for new source-of-truth pages and include enough initial structure that later agents can patch them safely.

## PM Storage Protection

The `.pm/` directory is PM storage, not a normal hand-edited project area.

- Do not manually edit files under `.pm/`.
- Do not manually move task state refs, edit task markdown files, rename or delete wiki files, or change PM metadata through filesystem edits.
- Use PM MCP tools for task creation, task state changes, task markdown updates, wiki page creation, wiki edits, wiki renames, wiki deletes, and project metadata changes.
- Direct `.pm/` reads are allowed for inspection only when useful, but direct `.pm/` writes are forbidden.
- If the PM MCP tool lacks an operation needed to update board or wiki state, stop and tell the user which PM MCP capability is missing.

When these instructions say that updating task markdown or editing the wiki is allowed, that means through the PM MCP tools only.

## Git Workflow

At the start of work, check whether Git is available and whether the worktree is clean.

- If the worktree is clean, proceed normally.
- If the worktree is dirty, notify the user before making changes.
- If the existing changes are obvious, coherent, and unrelated to ambiguity, make a commit for them before starting new work.
- If the existing changes are complex, mixed, surprising, or not obviously safe to commit, stop and ask how to proceed.
- Never overwrite, revert, or discard user changes unless explicitly instructed.
- Keep commits focused on completed work. Do not mix unrelated tasks into one commit.
- Prefix task commits with the PM task ID in square brackets, for example `[COMBAT-001] Add default rifle definition`.
- Branches may be created when they materially reduce risk or help organize larger work, but a branch per task is not required and should not be treated as the default workflow.

If Git is not initialized in the workspace, say so and continue with the PM board and wiki workflow.

## Wiki Is Source of Truth

The PM wiki is a project source of truth and must be kept current.

Agents working on features are responsible for updating wiki pages when behavior, architecture, setup, protocols, data formats, workflow, or constraints change. Creating new wiki pages and editing existing pages is allowed and expected.

Important wiki pages:

- `project-overview` - product goals, MVP scope, technology stack, and non-goals
- `architecture` - runtime architecture, authority boundaries, data flow, networking, physics, testing, and deployment shape

Before making architecture or gameplay-affecting changes, read the relevant wiki page. After the change, update the wiki if the documented source of truth is no longer accurate.

## Project Philosophy

This is an experimental cross-platform, server-authoritative battle royale built from the ground up in .NET.

The goal is a small complete multiplayer game loop, not a general-purpose engine. Prefer concrete, inspectable systems that serve the MVP over speculative abstractions.

The first complete version should prove:

- A Linux dedicated server can run headlessly.
- macOS and Linux clients can connect to the same server.
- Windows client support can be added later without changing core gameplay contracts.
- Players can move, fight, be eliminated, spectate, produce a winner, and reset into another match.
- The server remains authoritative over gameplay state.

## Architectural Boundaries

Preserve the separation between authority and presentation.

- The server owns authoritative simulation, movement validation, combat, health, ammunition, safe-zone state, match phases, eliminations, winners, and reset.
- The client owns windowing, input devices, rendering, audio or visual feedback, local prediction, interpolation, reconciliation display, and development UI.
- Shared simulation code may exist so server and client prediction use the same rules, but sharing code must not weaken server authority.
- Rendering, SDL windowing, SDL GPU, ImGui, and client UI must not become server dependencies.
- Client input represents intent, not authoritative state.

When ownership is unclear, default gameplay-relevant state to the server.

## Expected Solution Shape

The planned solution may include projects similar to:

```text
src/
  Game.Client/
  Game.Server/
  Game.Simulation/
  Game.Protocol/
  Game.Content/
  Game.Platform/
  Game.Rendering/
  Game.Debugging/
  Box3D.Bindings/
  Box3D/

tests/
  Game.Simulation.Tests/
  Game.Protocol.Tests/
  Game.Server.Tests/
  Box3D.Tests/

native/
  box3d/

assets/
  shaders/
  meshes/
  textures/
  maps/
```

Split projects only when there is a meaningful dependency, testing, or deployment boundary. Do not create a theoretical hierarchy that makes the code harder to move through.

## .NET CLI Usage

The project targets .NET 10.

In Codex or sandboxed sessions, run .NET commands that build in single-node mode and without restore once dependencies have already been restored:

```text
dotnet build <solution>.slnx -m:1 --no-restore
dotnet test <solution>.slnx -m:1 --no-restore
```

After fetching SDL3-CS from source, restore with the desktop target property so the fetched binding does not require Android workloads:

```text
dotnet restore <solution>.slnx -p:CI_DONT_TARGET_ANDROID=1
```

Any command that requires NuGet package access or restore may require an elevated shell in sandboxed environments.

Server smoke tests with `OTEL_EXPORTER_OTLP_ENDPOINT` set may hang inside the Codex sandbox even when the same command succeeds normally outside it. If an OTLP-enabled `dotnet run` smoke appears stuck with no server output, treat it as likely sandbox networking/exporter behavior: stop the stuck process, rerun the same command with an elevated shell, and record whether the elevated run passes before diagnosing application code.

Do not invent build, lint, format, or test commands. If no command exists yet, say so. Once commands are introduced, document them here and in the wiki when appropriate.

Client shader builds require the `shadercross` executable to be available on `PATH`. The client project compiles HLSL sources under `src/Royale.Client/Shaders/` to SPIR-V (`.spv`) and Metal (`.msl`) outputs after build, and copies the original HLSL files for Direct3D/DXIL-facing development until a DXIL flow is explicitly chosen. `SDL_shadercross` is a local build tool dependency and is not vendored through `thirdparty`.

## Native Dependencies

The project uses SDL3, SDL GPU, Box3D, and ImGui-related bindings or integration.

- Keep native dependency layout explicit and consistent across supported runtime identifiers.
- Pin native dependency versions once they are chosen.
- Keep Box3D bindings focused on the API surface needed by the game.
- Verify native memory layouts for C# bindings with tests.
- Package only the native libraries required by each artifact.
- The Linux server package must not depend on SDL GPU, ImGui, textures, client shaders, or graphics initialization.

If a native dependency decision is unclear, ask before assuming.

## Networking and Protocol Discipline

Keep networking boundaries explicit.

- Real UDP transport, in-process transport, test transport, and simulated-loss transport should preserve the same message flow.
- Clients send input commands and connection messages.
- Servers send authoritative snapshots and events.
- Protocol messages should include versioning and enough identity, sequencing, and acknowledgement data to debug behavior.
- Favor simple full snapshots early. Optimize only after behavior is correct and measured.
- Protocol-incompatible clients and servers should fail clearly.

Do not let the client call arbitrary server gameplay methods directly in in-process mode.

## Simulation and Gameplay Discipline

Simulation should be deterministic enough for prediction and reconciliation to stay understandable, but do not overbuild determinism before the MVP needs it.

- Use a fixed simulation tick for server authority.
- Keep render rate separate from simulation rate.
- Bound catch-up ticks so stalls do not create uncontrolled simulation spirals.
- Keep the player controller shared between server and client prediction where practical.
- The initial player controller is a kinematic capsule, not a freely simulated dynamic rigid body.
- The initial weapon is a server-authoritative hitscan rifle.
- The initial synchronized gameplay objects are players, static map collision, weapon pickups, and safe-zone state.

Gameplay behavior changes must be reflected in tests and wiki documentation.

## Rendering and UI Principles

The renderer should remain thin and game-specific.

Initial rendering needs are static meshes, depth testing, camera matrices, basic lighting, debug geometry, and ImGui. There is no initial requirement for deferred rendering, render graphs, material graphs, dynamic global illumination, streaming terrain, or generalized scene editing.

ImGui is development tooling, not the final player-facing interface.

Diagnostics should expose:

- Frame and simulation timing
- Client and server ticks
- Snapshot buffering
- Prediction corrections
- Input queue depth
- Physics step timing
- Player state
- Weapon state
- Safe-zone and match state
- Packet counts, loss, latency, jitter, and invalid packets

## Testing Expectations

Add tests at the level where the behavior lives.

Expected test areas include:

- Protocol serialization and version handling
- Input buffering and sequence comparisons
- Match-state transitions
- Safe-zone interpolation and damage
- Weapon fire cadence and damage rules
- Box3D structure layouts and binding behavior
- Player movement collision cases
- Headless server simulation
- In-process client/server integration
- Consecutive match reset behavior

For cross-platform or native work, document what was verified locally and what remains to be verified on other platforms.

## Implementation Discipline

- Inspect the wiki, task, and nearby code before changing behavior.
- Make the smallest coherent change that satisfies the selected task.
- Preserve existing naming, file organization, dependency direction, and testing style.
- Do not introduce engine systems without a concrete gameplay need.
- Avoid unrelated refactoring.
- Remove obsolete code introduced by your change.
- Keep user-controlled data escaped in any UI or generated HTML.
- Do not commit generated artifacts such as `bin/`, `obj/`, `node_modules/`, build outputs, local databases, or packaged native artifacts unless the task explicitly requires committed fixtures.
- Explain significant architectural deviations in the task notes and wiki.

## Documentation Requirements

Documentation is part of completion.

Update the wiki when changing:

- Architecture or dependency direction
- Build, restore, test, package, or deployment workflow
- Native dependency versions or layout
- Protocol messages or compatibility rules
- Simulation tick order or timing
- Map or content formats
- Gameplay rules
- Server authority boundaries
- Diagnostics or debugging workflows

If a task changes behavior but the wiki remains accurate, note that no wiki update was needed in the task or final summary.

## Validation Checklist

Before moving a task to `done`, verify:

- The selected PM task is the work that was actually completed.
- The implementation follows the documented architecture.
- Ambiguous behavior was clarified with the user instead of assumed.
- Relevant tests were added or updated.
- Relevant build and test commands were run, or unavailable commands were explicitly noted.
- The wiki was updated if source-of-truth documentation changed.
- No direct `.pm/` storage edits were made.
- Native and cross-platform implications were considered.
- The server remains free of client rendering and UI dependencies.
- No unrelated changes or generated artifacts were introduced.

## Human Validation Requests

When a completed change would benefit from project-owner validation, call that out explicitly in the final summary.

This is required for changes that cannot be fully validated by automated tests, including rendering appearance, game feel, input behavior, camera feel, combat feel, platform-specific behavior, audio or visual feedback, and major UI or debug tooling changes.

Still run the relevant automated validation first. Human validation is an additional request, not a substitute for tests.

When starting a dedicated server for project-owner validation, run it in an elevated shell with OTLP export enabled so logs, metrics, and traces flow into the local observability stack. Prefer the local Collector endpoint when available:

```text
OTEL_EXPORTER_OTLP_ENDPOINT=http://127.0.0.1:4317 dotnet run --project src/Royale.Server/Royale.Server.csproj --no-restore -- <server arguments>
```

Do not try OTLP-enabled validation server runs in the Codex sandbox first; they are expected to hang there. Request an elevated shell from the start before diagnosing application code.

Be specific about what should be validated and how. For example: "Please validate F5-F8 render modes visually" or "Please play a short combat loop and check that rifle cadence feels acceptable."

## Review Guidance

When reviewing changes, prioritize:

1. Correctness and server authority
2. Data safety and protocol compatibility
3. Cross-platform behavior
4. Test coverage
5. Wiki accuracy
6. Dependency direction
7. Debuggability and observability
8. Performance based on measurement
9. Simplicity and maintainability
10. Visual and interaction polish

Flag client authority leaks, undocumented protocol changes, rendering dependencies in server code, speculative engine abstractions, missing wiki updates, missing tests, unsafe native interop, unclear ownership, and changes made without asking when requirements were ambiguous.
