# Island Topology Triangulation Notes

## Decision

The island top surface now uses `BurstTriangulator` as the only triangulation backend.

Implementation choice:

- keep the drawn spline and sampled outline as the source-of-truth boundary
- validate and normalize that boundary before triangulation
- seed interior points explicitly from the single `spacing` control
- run constrained Delaunay triangulation against the clean boundary plus those interior points
- extrude the clean-perimeter top straight down to form the island volume

This keeps the island shape artist-authored while producing interior vertices that are much more useful for vertex painting and displacement than a boundary-only fan.

## Boundary Validation

The sampled outline is still validated before triangulation, but self-intersection handling is now done through `Clipper2` canonicalization instead of only a custom float segment-pair test.

Current approach:

- remove duplicate and collinear points from the sampled loop
- sample smooth spline sections by world-space spacing without automatically pinning every knot into the mesh outline
- still force samples at geometric corners so hard authored breaks survive coarse spacing
- run a `Clipper2` union on the single closed path at high decimal precision
- accept the result only when it resolves to one dominant filled region with negligible area drift
- reject the outline when canonicalization produces multiple meaningful regions or a large area change

Why:

- small spacing changes on near-miss concave silhouettes were producing false self-intersection failures with tolerance-heavy pairwise segment checks
- `Clipper2` already ships in the repo, is integer-backed internally, and is explicitly designed for complex and self-intersecting polygon cleanup
- this keeps real invalid topology rejected while making authoring much less sensitive to sample placement

## Why This Over Ear Clipping

Ear clipping only triangulates the existing boundary vertices. It can change diagonals, but it does not create a useful interior vertex distribution.

For this tool, that meant:

- too many cap triangles visually converged around a small set of points
- the top surface was poor for paint masks and displacement
- changing the outline improved silhouette but not top-surface workability

Seeded constrained Delaunay solves the actual problem:

- it preserves the drawn boundary
- it adds interior sample points without forcing extra boundary splits
- it produces more even triangle shapes than ear clipping
- it keeps the visible cap perimeter and wall silhouette aligned to the same clean polygon

## Library Choice

Chosen package:

- Package: `com.andywiecko.burst.triangulator`
- Source: [BurstTriangulator](https://github.com/andywiecko/BurstTriangulator)
- Version: `v3.9.1`
- License: [MIT](https://github.com/andywiecko/BurstTriangulator/blob/v3.9.1/LICENSE.md)

Why this package:

- Unity-native package layout
- supports constrained triangulation
- handles extra interior points directly in the input positions buffer
- exposes halfedge output so the output boundary can be verified against the authoring polygon

Rejected option:

- [Triangle.NET](https://github.com/wo80/Triangle.NET) was not selected because its repository warns about unclear commercial licensing inherited from Triangle.

## Runtime Settings

The runtime builder uses the following triangulation settings:

```csharp
RestoreBoundary = true;
RefineMesh = false;
AutoHolesAndBoundary = false;
ValidateInput = false;
Verbose = false;
```

Notes:

- `RestoreBoundary = true` keeps the triangulated cap anchored to the provided clean boundary constraints.
- `ValidateInput = false` is intentional because the island builder already validates the sampled polygon before triangulation, and the package docs call input validation expensive.
- `spacing` remains the single user-facing density control. It affects both boundary sampling and the interior point seed density.

## Interior Seeding

The cap now gets its density from an explicit staggered interior seed pattern instead of triangle refinement.

Current approach:

- generate a hex-like staggered grid inside the polygon bounds
- use `spacing` as the horizontal seed interval
- use `sqrt(3) / 2 * spacing` as the row interval
- reject any candidate that is outside the polygon or too close to the boundary
- fall back to a centroid seed when the polygon has usable interior area but the grid produces no interior samples

Why use this:

- it keeps the boundary completely driven by the validated authoring polygon
- it still gives predictable interior density from the same artist-facing spacing control
- it avoids the arbitrary one/two/four-way perimeter splits that refinement introduced around thin or acute features

## Boundary Reconstruction

The top cap is built from the triangulator output positions and triangles directly.

The triangulator boundary halfedges are still reconstructed:

- every halfedge where `Output.Halfedges[id] == -1` is part of the triangulated boundary
- those directed edges are stitched into a single cycle
- the cycle is normalized to counter-clockwise winding before wall extrusion
- if the result is not exactly one outer cycle, island generation fails instead of guessing

Current usage:

- the output boundary loop is compared against the validated authoring polygon
- generation fails instead of silently accepting a cap that split or drifted the perimeter
- the top cap perimeter and visible side walls both stay on the same clean authoring boundary

Why:

- the main user-facing baseline is now `Bezier outline -> spacing-driven clean mesh outline -> interior meshing`
- the interior can stay flexible without letting triangulation own the silhouette

## What Stays Unchanged

- `IslandShape` remains the root authoring component
- the spline outline remains the only shape source of truth
- `TryGetResolvedTopPolygon` still returns the validated authoring boundary polygon, not the refined cap topology
- the island still extrudes straight down to a flat bottom

## Primary References

- [BurstTriangulator README](https://github.com/andywiecko/BurstTriangulator/blob/main/README.md)
- [Constrained triangulation example](https://github.com/andywiecko/BurstTriangulator/blob/main/Documentation~/manual/examples/constrained-triangulation.md)
- [Halfedge output docs](https://github.com/andywiecko/BurstTriangulator/blob/main/Documentation~/manual/advanced/output-halfedges.md)
- [Package manifest for v3.9.1](https://github.com/andywiecko/BurstTriangulator/blob/v3.9.1/package.json)
- [MIT license for v3.9.1](https://github.com/andywiecko/BurstTriangulator/blob/v3.9.1/LICENSE.md)
