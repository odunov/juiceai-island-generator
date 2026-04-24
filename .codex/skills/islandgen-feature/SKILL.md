---
name: islandgen-feature
description: Implement or extend coast-first island generator features in this repository. Use when changing the Unity island authoring stack under `Assets/IslandShape`, especially for new generator subsystems, coast-section behavior, side profiles, water integration, editor tooling, tests, or architecture-aligned refactors that must stay consistent with the docs in `Docs/IslandGen`.
---

# IslandGen Feature

## Overview
Implement island-generator changes in a way that preserves the repo's coast-first architecture, keeps runtime/editor/tests/docs in sync, and avoids one-off feature code that drifts from the project bible.

## Read First
- Read `Docs/IslandGen/Specs/v0.2-coast-water.md` before changing behavior.
- Read `Docs/IslandGen/Architecture/island-art-tech-bible.md` when the task affects visual rules, topology, coast profiles, beach behavior, or water.
- Read `Docs/IslandGen/Roadmap/version-roadmap.md` when the request may belong in a later version.

## Working Rules
- Keep `IslandShape` as the root authoring component and spline source of truth.
- Prefer dedicated generator or service classes over stuffing more responsibilities into `IslandShape` or one giant builder.
- Keep the system game-local under `Assets/IslandShape`, but preserve package-ready boundaries.
- Update runtime, editor, tests, and docs together when the feature meaningfully changes behavior.
- Preserve legacy behaviors only when the roadmap or active mode explicitly requires them.
- Do not leave the repo in a half-migrated state. If a feature is interrupted, restore a buildable boundary before stopping.

## Default Workflow
1. Read the relevant docs and inspect the affected runtime/editor/tests.
2. Decide whether the work belongs to:
   - authoring root/data model
   - runtime generation
   - water/runtime visuals
   - editor tooling
   - tests
   - docs
3. Add or update the smallest coherent subsystem that fits the architecture.
4. Wire the editor and authoring surface after the runtime model is stable.
5. Add or update regression coverage for the new behavior.
6. Run at least a compile verification before finishing.

## Subsystem Expectations
### Runtime
- Put major geometry factors in their own generator or utility classes.
- Keep `IslandMeshBuilder` as orchestration glue, not the only place where behavior lives.
- Favor predictable artist controls over hidden coupled parameters.
- Use Clipper2 for robust polygon cleanup, offsets, unions, and ring resolution when geometry gets tight.

### Editor
- Keep inspector layout organized around workflow first, then settings.
- Keep Scene view interactions planar on the island's local XZ authoring plane.
- Hide legacy or advanced controls when they are not relevant to the active mode.

### Tests
- Add targeted editor tests for geometry validity, predictable control behavior, and regressions in coast/topology rules.
- Prefer tests that exercise outputs artists care about: continuity, silhouette stability, triangle validity, and mode-specific behavior.

### Docs
- Update the spec or roadmap when a feature meaningfully shifts scope, naming, or architecture.
- Keep docs concise and decision-oriented.

## Guardrails
- Do not introduce high-frequency noise as a substitute for authored form.
- Do not add ad hoc per-island hacks when a coastline-derived rule or section-based rule is more appropriate.
- Do not let new features bypass the existing validation and rebuild flow.
- Do not add new public authoring primitives if the roadmap says that belongs to a later version.

## Done Criteria
- The feature matches `v0.2` terminology and architecture.
- Runtime/editor/tests/docs are aligned.
- The project compiles.
- The change leaves a clean next step instead of a dangling partial migration.
