---
name: royale-source-control-implementation
description: Royale git workflow and implementation discipline. Use when starting code changes, handling dirty worktrees, committing, protecting user changes, avoiding generated artifacts, final summaries, docs completion, or human validation requests.
---

# Royale Source Control and Implementation Discipline

Use this skill for git workflow, dirty worktree handling, implementation discipline, generated artifact decisions, documentation completion, validation summaries, and human validation requests.

## Git workflow

At the start of work, check whether Git is available and whether the worktree is clean.

- If the worktree is clean, proceed normally.
- If the worktree is dirty, notify the user before making changes.
- If the existing changes are obvious, coherent, and unrelated to ambiguity, make a commit for them before starting new work.
- If the existing changes are complex, mixed, surprising, or not obviously safe to commit, stop and ask how to proceed.
- Never overwrite, revert, or discard user changes unless explicitly instructed.
- Keep commits focused on completed work.
- Do not mix unrelated tasks into one commit.
- Prefix task commits with the PM task ID in square brackets, for example `[COMBAT-001] Add default rifle definition`.
- Branches may be created when they materially reduce risk or help organize larger work, but a branch per task is not required and should not be treated as the default workflow.

If Git is not initialized in the workspace, say so and continue with the PM board and wiki workflow.

## Implementation discipline

- Inspect the wiki, task, and nearby code before changing behavior.
- Make the smallest coherent change that satisfies the selected task.
- Preserve existing naming, file organization, dependency direction, and testing style.
- Do not introduce engine systems without a concrete gameplay need.
- Avoid unrelated refactoring.
- Remove obsolete code introduced by your change.
- Keep user-controlled data escaped in any UI or generated HTML.
- Do not commit generated artifacts such as `bin/`, `obj/`, `node_modules/`, build outputs, local databases, or packaged native artifacts unless the task explicitly requires committed fixtures.
- Explain significant architectural deviations in task notes and wiki.

## Documentation requirements

Documentation is part of completion.

Update the wiki when changing:

- Architecture or dependency direction.
- Build, restore, test, package, or deployment workflow.
- Native dependency versions or layout.
- Protocol messages or compatibility rules.
- Simulation tick order or timing.
- Map or content formats.
- Gameplay rules.
- Server authority boundaries.
- Diagnostics or debugging workflows.

If a task changes behavior but the wiki remains accurate, note that no wiki update was needed in the task or final summary.

## Validation completion

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

Use `royale-build-validation` for command selection.
Use `royale-pm-workflow` before moving task state.

## Human validation requests

When a completed change would benefit from project-owner validation, call that out explicitly in the final summary.

This is required for changes that cannot be fully validated by automated tests, including:

- Rendering appearance.
- Game feel.
- Input behavior.
- Camera feel.
- Combat feel.
- Platform-specific behavior.
- Audio or visual feedback.
- Major UI or debug tooling changes.

Still run relevant automated validation first. Human validation is an additional request, not a substitute for tests.

When starting a dedicated server for project-owner validation, run it in an elevated shell with OTLP export enabled so logs, metrics, and traces flow into the local observability stack. Prefer the local Collector endpoint when available:

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=http://127.0.0.1:4317 dotnet run --project src/Royale.Server/Royale.Server.csproj --no-restore -- --config config/server.development.json
```

Use explicit CLI arguments after `--config` only for one-off validation overrides. Launch settings merge as built-in defaults, selected JSON profile, then explicit CLI arguments.

Do not try OTLP-enabled validation server runs in the Codex sandbox first; they are expected to hang there. Request an elevated shell from the start before diagnosing application code.

Be specific about what should be validated and how, for example:

- `Please validate F5-F8 render modes visually.`
- `Please play a short combat loop and check that rifle cadence feels acceptable.`

## Final summary shape

When finishing implementation, summarize:

- What changed.
- Which PM task was completed or updated.
- Which tests/builds ran.
- Whether the wiki was updated or why no update was needed.
- Any human validation requested.
- Any known limitations or follow-up PM tasks created.
