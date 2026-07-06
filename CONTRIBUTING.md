# Contributing

Royale is an experimental, agent-assisted game prototype. Contributions should keep the project small, explicit, and aligned with the server-authoritative architecture documented in the PM wiki.

## Start Here

Read `AGENTS.md` before making changes. It is the repository's detailed workflow and engineering guide for both human contributors and coding agents.

The most important rule is: ask before assuming. If requirements, gameplay behavior, architecture, platform policy, protocol shape, native dependency choices, or PM/wiki changes are unclear, ask the project owner before turning an assumption into project behavior.

## PM Workflow

Feature work is driven from the PM board in `.pm/`.

- Choose or create a PM task before implementation.
- Move the selected task to `doing` while working.
- Keep the task notes updated when decisions, validation, blockers, or follow-up work matter.
- Move the task to `done` only after implementation, validation, and documentation are complete.
- Do not edit `.pm` storage manually when PM tools are available.

The PM wiki is a source of truth. Update it when behavior, architecture, setup, protocol, data formats, third-party dependencies, or workflow changes.

## Development Expectations

- Preserve server authority. Client input represents intent, not authoritative game state.
- Keep rendering, SDL windowing, SDL GPU, ImGui, and client UI out of the dedicated server.
- Prefer the smallest coherent change that satisfies the selected task.
- Avoid speculative engine systems, broad refactors, or new abstractions without a concrete gameplay need.
- Add tests at the layer where behavior lives.
- Run the relevant build and test commands before marking work complete, or clearly document why they were not run.

## Third-Party Dependencies

Third-party source is managed through `thirdparty/`, not Git submodules.

- Pin repositories by full commit SHA in `thirdparty/versions.env`.
- Fetch source into ignored `thirdparty/repos/` directories.
- Keep project-specific changes as ordered patches under `thirdparty/patches/<dependency>/`.
- Ask before changing dependency pins, adding dependencies, or introducing non-obvious patches.

See `thirdparty/README.md` and `.pm/wiki/third-party-dependencies.md` for the full policy.

## Commits

Keep commits focused. For task work, prefix commit messages with the PM task ID, for example:

```text
[RENDER-010] Load crate mesh with SimpleMesh
```

Do not commit generated outputs such as `bin/`, `obj/`, third-party clones, native build artifacts, local databases, or packaged builds unless a task explicitly calls for a committed fixture.
