---
title: Agent Workflow
createdAt: 2026-07-11T06:09:29.0533370Z
modifiedAt: 2026-07-12T18:32:29.5383380Z
---

## Purpose

Royale is developed with autonomous coding agents operating under explicit project decision gates. `AGENTS.md` is the always-on repository contract. Domain-specific procedures live under `.agents/skills/`.

The instructions are model-agnostic. They should favor grounded autonomy rather than encode behavior for one model version.

## Grounded Autonomy

Agents inspect repository code, tests, PM data, and relevant wiki pages before asking questions.

Agents ask the project owner only when an answer cannot be discovered locally and the decision would establish or change a project contract, including gameplay rules, protocol compatibility, file formats, rendering or physics behavior, platform policy, dependency selection, and authority ownership.

Routine implementation details and choices already established by nearby code or the wiki do not require confirmation. Agents choose the smallest implementation consistent with existing patterns.

## Task Lifecycle

Implementation work is driven through PM MCP:

1. Search for and inspect the task and dependencies.
2. Create a narrow task only when no suitable task exists.
3. Move the selected task to `doing` before tracked implementation edits.
4. Keep implementation within task scope.
5. Record decisions, validation, limitations, and human-validation requirements.
6. Update source-of-truth wiki pages in the same task.
7. Validate PM and move the task to `done` only when required work is complete.

Planning and review may inspect PM without moving task state. Files under `.pm/` are never mutated directly.

## Git And Validation

Agents check the worktree before implementation. One obvious coherent pre-existing change is reported and committed before new work; mixed or ambiguous changes require owner direction. Existing work is never discarded implicitly.

Task commits use `[TASK-ID] Imperative summary`. Branches are optional and used only when they materially reduce risk.

Validation scales with blast radius. Exact commands and outcomes are recorded. Rendering, UI, platform behavior, audiovisual feedback, camera feel, movement feel, and combat feel require explicit owner validation in addition to automated checks.

Client and editor screenshot validation commands request `.png` output directly. BMP paths are intentionally rejected and must not be used as compatibility aliases or converted after capture.

## Skill Design

Repository skills use progressive disclosure:

- `AGENTS.md` owns invariants and routing.
- PM workflow owns PM and wiki operations.
- Source control owns implementation and commit discipline.
- Build validation owns commands and environment-specific validation.
- Domain skills contain only architecture, rendering/native, networking/protocol, simulation/gameplay, or review guidance.

Agents load the smallest relevant skill set and do not preload unrelated domains. Skill descriptions are the primary routing surface and should remain specific enough to avoid accidental activation.

Project folders and namespace suffixes remain aligned. Cross-domain references use explicit file-level `using` directives; project-wide global usings are not used.