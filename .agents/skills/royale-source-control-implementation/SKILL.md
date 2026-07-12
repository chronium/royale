---
name: royale-source-control-implementation
description: Execute Royale repository changes safely. Use when starting implementation, handling a dirty worktree, protecting existing edits, moving files, deciding branch/commit scope, avoiding generated artifacts, completing documentation, requesting human validation, or preparing the final report.
---

# Royale Source Control And Implementation

## Start Safely

Run `git status --short --branch` before implementation.

- Clean: proceed.
- Dirty with one obvious coherent change: notify the owner, inspect it, validate as appropriate, and commit it before new work.
- Dirty with mixed, surprising, generated, or ambiguous changes: stop and ask.

Never reset, discard, overwrite, or “clean up” existing work without explicit instruction. Work with concurrent user changes when they are relevant. Ignore unrelated changes rather than reverting them.

Confirm the PM task is `doing` before editing tracked implementation files.

## Implement Deliberately

- Read nearby code/tests and the relevant wiki before changing behavior.
- Make the smallest coherent change that satisfies the task.
- Preserve project folders, matching namespaces, explicit file-level imports, dependency direction, naming, disposal, and result/error patterns.
- Keep declarations and control flow readable. Do not compress multiple unrelated statements, fields, assertions, or lifecycle steps onto one line even when the formatter accepts it.
- Keep files and methods cohesive. Split ownership-focused helpers or domain behavior before a file becomes a monolith; do not replace one large file with a gratuitous swarm of tiny abstractions.
- Keep composition roots centered on wiring and lifecycle. Place substantial behavior in the domain folder and namespace that owns it.
- Add interfaces only for actual polymorphism, dependency isolation, platform boundaries, or valuable test seams. Prefer a concrete type when there is one implementation and no substitution need.
- Avoid unrelated refactors and dependency additions.
- Use `apply_patch` for manual edits; bulk mechanical moves/rewrites may use appropriate deterministic commands.
- Do not commit `bin/`, `obj/`, `node_modules/`, local databases, native build outputs, or packaged artifacts unless they are intentional fixtures.

Branches are optional. Use one only when isolation materially reduces risk or supports a requested experiment; do not create a branch per task by habit.

## Complete The Task

1. Inspect the diff and confirm it matches the PM scope.
2. Run documented validation and report exact outcomes.
3. Inspect changed files for readability that automated formatting cannot enforce: compressed statements, oversized methods/files, misplaced cross-domain behavior, and unnecessary abstractions.
4. Update task notes and wiki source-of-truth content through PM MCP.
5. Request owner validation for visuals, UI, platform behavior, audio, camera, movement, or combat feel.
6. Validate PM when PM/wiki structure changed and move the task to `done`.
7. Run `git diff --check`, stage intentionally, inspect the staged diff, and commit.

Commit messages begin with the task ID: `[TASK-123] Imperative summary`.

Final reports state what changed, PM task status, validation, wiki status, commit, limitations, and any human follow-up. Do not claim completion when required validation or documentation remains unfinished.
