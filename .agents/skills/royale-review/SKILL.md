---
name: royale-review
description: Review Royale changes without implementing them. Use for code review, diff/commit/branch review, completed-agent validation, regression analysis, authority or dependency leaks, protocol/native risk, missing tests, PM/wiki omissions, or cross-platform concerns.
---

# Royale Review

## Review Method

Read the task and relevant wiki, then inspect the diff in context. Review behavior and contracts, not formatting preference. Run focused checks when they materially confirm or reject a suspected issue.

Report findings first, ordered by severity. Each finding should identify the concrete failure/risk, why it matters, and a precise file/line reference. Do not lead with a summary.

If no findings exist, say so explicitly and list remaining test, platform, native, visual, or game-feel risk.

## Priority Order

1. Correctness, data safety, crashes, leaks, and server authority.
2. Protocol compatibility, validation, identity, sequencing, and malformed input.
3. Native ABI, ownership, disposal, and platform behavior.
4. Gameplay regressions, prediction/reconciliation divergence, and determinism.
5. Missing tests, diagnostics, PM notes, or wiki updates.
6. Maintainability issues that create a concrete future defect risk.

## Boundary Checks

- Server code remains headless and free of client rendering/UI dependencies.
- Client commands express intent rather than authoritative outcomes.
- Shared simulation remains presentation-independent.
- Real, in-process, scripted, and impaired network paths preserve the intended boundary.
- Wire-layout changes update bounds/tests and follow the recorded compatibility decision.
- Folder paths and namespaces match; cross-domain imports are explicit rather than project-wide global usings.
- Native handles have clear ownership and failure paths.
- Generated artifacts and secrets are absent.

## Completion Checks

- Work matches the selected PM task and dependencies.
- PM changes used MCP rather than direct `.pm/` writes.
- Behavior/architecture/setup changes updated the wiki.
- Validation is appropriate to the blast radius and its output is credible.
- Human validation is requested for visual or feel-dependent behavior.

Separate confirmed findings from open questions and assumptions. Keep the final change summary secondary and brief.
