---
id: EDITOR-023
title: Generate and cache model thumbnails
track: EDITOR
milestone: M6
priority: medium
dependsOn:
- EDITOR-002
- EDITOR-020
- EDITOR-021
- EDITOR-022
createdAt: 2026-07-12T09:08:09.6076830Z
modifiedAt: 2026-07-12T18:46:36.3996690Z
---

Generate lazy 256×256 model thumbnails for active .royaleproject editor sessions using Royale.Rendering offscreen rendering, elevated three-quarter camera framing, opaque neutral-gray backgrounds, and asynchronous SDL GPU readback. Cache previews as deterministic PNG files under .cache/thumbnails using renderer/source/resource fingerprints, bounded per-frame scheduling, corruption recovery, stale cleanup, failure suppression, and strict GPU resource disposal.

Pin StbImageWriteSharp 1.16.7 and StbImageSharp 2.30.15 centrally and reference them only from Royale.Rendering for public-domain PNG encoding and decoding. Replace BMP screenshot support entirely with PNG encoding/decoding over RGBA buffers, require .png screenshot paths during client/editor option validation, remove BMP code/tests/docs/examples without compatibility aliases, and document the dependency pin/license. Integrate the preview provider only for active project sessions, preserve asset-browser state during provider replacement, report concise failures, and keep generated runtime artifacts independent.

Cover framing, fingerprints, PNG codec/atomic writes, option rejection, scheduling/cache/disposal, and opt-in native GPU readback/upload. Validate affected projects and the full solution, capture client/editor PNG screenshots, exercise a temporary Kenney-model project and cache reuse, update relevant wiki pages, and request owner visual validation.

## Notes

- 2026-07-12 18:21 UTC - Implementation started from the approved handoff plan. Dependency restore succeeded, but the first full build is blocked before compilation because SixLabors.ImageSharp 4.0.0 enforces a Six Labors license and the repository provides neither `SixLaborsLicenseKey` nor `SixLaborsLicenseFile`/`sixlabors.lic`. Exact failure: `No Six Labors license found` from `SixLabors.ImageSharp.targets`. Owner decision/credential is required before continuing because changing the requested version or dependency would alter the dependency and licensing contract. Task remains in doing.
- 2026-07-12 18:28 UTC - Owner authorized replacing the license-enforced ImageSharp 4.0.0 dependency. The implementation now uses centrally pinned StbImageWriteSharp 1.16.7 for PNG encoding and companion StbImageSharp 2.30.15 for cache decoding, both isolated to Royale.Rendering. Work resumed.
- 2026-07-12 18:46 UTC - Implemented PNG-only screenshots and cached model previews. Dependency decision: centrally pinned StbImageWriteSharp 1.16.7 and StbImageSharp 2.30.15, referenced only by Royale.Rendering; removed ImageSharp/BMP code and examples. Added deterministic RGBA PNG codec/atomic writes, .png launch validation, 256×256 bounds-derived thumbnail framing, source/resource/settings fingerprints, active-project-only preview lifecycle, bounded per-frame cache/upload/render work, SDL fence waiting off the interaction thread, owned sampled-texture upload, corruption regeneration, stale cleanup, and failure reporting.

  Validation:
  - dotnet restore Royale.slnx -p:CI_DONT_TARGET_ANDROID=1 — succeeded.
  - dotnet format Royale.slnx --no-restore --verify-no-changes --include affected source/test projects — succeeded (workspace-load warning only).
  - dotnet build Royale.slnx -m:1 --no-restore — succeeded with 0 warnings/errors.
  - dotnet test Royale.slnx -m:1 --no-restore --no-build — all projects passed (1,077 tests).
  - Affected post-final-edit tests: Royale.Editor.Tests 92/92 and Royale.Rendering.Tests 89/89 passed.
  - ROYALE_GPU_TESTS=1 rendering integration test — passed, covering offscreen readback and sampled RGBA upload.
  - Client captured /tmp/editor-023-client.png directly as a valid 1920×1080 RGBA PNG.
  - Temporary Kenney-only graybox.royaleproject generated 10 valid 256×256 cache PNGs and /tmp/editor-023-editor-worker.png showed the populated browser.
  - Restart captured /tmp/editor-023-editor-cache-hit.png with cache mtimes unchanged.
  - Truncated the crate cache and added a stale crate PNG; editor logged the corrupt cache once, regenerated a valid 256×256 PNG, and removed the stale file.
  - git diff --check passed.

  Wiki updated: content/rendering, editor, runtime processes, diagnostics, agent workflow, and third-party pins. Owner visual validation remains requested for thumbnail framing/readability, progressive loading, grid density, and interaction smoothness.