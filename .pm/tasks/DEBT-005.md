---
id: DEBT-005
title: Organize project files by domain namespaces
track: DEBT
milestone: M6
createdAt: 2026-07-11T05:28:17.7507100Z
modifiedAt: 2026-07-11T05:37:07.3172050Z
---

Reorganize production and test C# files that have accumulated in project roots into coherent domain folders and matching namespaces. Cover the whole solution while preserving existing project/assembly boundaries, runtime behavior, public protocol and serialization contracts, server authority, and deployment shape. Update references and tests mechanically, document the resulting source layout in the architecture wiki, and validate the full solution.

## Notes

- 2026-07-11 05:37 UTC - Completed the whole-solution file and namespace reorganization.

  - Production domain folders now cover protocol, network, server, content, diagnostics, Box3D, Box3D bindings, and the asset pipeline; existing client and simulation domains were preserved.
  - Test projects now mirror production domains, with shared Box3D fixtures isolated under Infrastructure.
  - Folder paths and namespace suffixes match. Cross-domain references use explicit file-level using directives; no project-wide global usings were introduced.
  - Assembly/project boundaries, protocol wire shapes, runtime behavior, server authority, and deployment dependencies are unchanged.
  - Updated architecture/overview Solution Structure as source of truth.
  - Validation passed: dotnet build Royale.slnx -m:1 --no-restore with 0 warnings/errors; dotnet test Royale.slnx -m:1 --no-restore with all 917 tests passing.
  - No human validation is required because this is a behavior-preserving source organization change.