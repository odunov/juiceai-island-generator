# Island Art + Tech Bible

## Thesis
This project targets stylized, shape-first island dioramas built from clear silhouettes, terraced chalky cliffs, soft painterly surface treatment, graphic shorelines, and sparse dressing. The reference image is a north star for readability, proportion, and material hierarchy, not a template to copy. The right direction is systems-first: the tool should generate pleasing landforms through strong shape rules, controlled irregularity, and a few deliberate rendering tricks instead of piling on noise, props, or simulation.

## Visual Pillars
- Silhouette first.
  The island must read clearly from far away before any material or prop detail is added.
- Broad rounded landmasses.
  Favor large, stable masses with a few memorable cuts, coves, and channels over jagged coastlines.
- Stepped cliff bands.
  Islands should feel carved into tiers and ledges, not like smooth hills extruded upward.
- Three main surface families.
  Keep the surface language simple: grass, sand, and stone.
- Clean shoreline read.
  The coast should be legible through a bright foam edge, shallow-water tint, and a beach or cliff decision that is obvious at a glance.
- Sparse props, low clutter.
  Dressing supports silhouette and scale only. It must not become the main attraction.
- Controlled wonk over random noise.
  Irregularity should feel authored and pleasant, not chaotic or perlin-heavy.

## Current Baseline
- The current source-of-truth system is `IslandShape` spline authoring feeding generated island meshes in [IslandShape.cs](/C:/ART/GameProjects/islands/Assets/IslandShape/Runtime/IslandShape.cs).
- The outline is authored as a planar spline locked to local XZ space.
- The generated mesh is currently a simple closed extrusion: the drawn top surface is refined into an evenly distributed constrained triangulation, extruded straight down to a flat bottom, and connected by vertical side walls.
- There are no terrace, coast-band, or edge-zone passes in the current generator.
- The current look is intentionally simple: one triplanar checker default island material, basic mesh output, no beach layering, no cliff stratification, no water treatment, no shoreline foam, and no dressing system yet.
- This is good. It means the next visual steps can build on a clear base instead of fighting old art decisions.

## Production Breakdown

### Shape Language
- Favor clustered, rounded island masses with a strong primary form and a few smaller supporting forms.
- Prefer gentle inward cuts, coves, necks, and channels over spiky outlines.
- Keep coastlines uneven enough to feel natural, but never noisy.
- Build memorable silhouette moments with broad negative space, detached islets, and a small number of accent stacks.
- Avoid thin peninsulas, sawtooth edges, and tiny repeated bumps.
- Avoid "realistic" erosion noise if it weakens the top-down read.

### Elevation and Cliffs
- Treat elevation as a small number of readable terrace bands, not a continuous sculpt.
- Cliff walls should read as stacked chalky slabs with soft breaks and occasional ledges.
- Rock stacks should be used as accents at edges, corners, or channel entries.
- Vertical variation should strengthen the silhouette, not fragment it.
- Future cliff logic should prefer broad stepping and band separation over micro-chipping or high-frequency rock detail.
- Avoid deep geological realism. These islands should feel designed, not simulated.

### Surface Distribution
- Grass owns the broad top surfaces and should be the default "resting" material.
- Sand appears in flatter, open, traversable zones and near friendly shoreline transitions.
- Stone is exposed on cliff walls, sharp breaks, undercuts, and structural edge zones.
- Do not introduce many extra surface types early. The power of the look comes from clean material hierarchy.
- Sand should help guide the eye through paths, clearings, and beaches, not cover everything.
- Grass-to-sand and sand-to-stone transitions should be broad and readable, never mottled.

### Shoreline and Water
- The shoreline is one of the main style carriers and must be treated as a graphic design problem.
- Every coast should clearly read as either beach-like or cliff-like.
- Add a bright foam line or light rim where water meets land.
- Add a shallow-water tint band near the coast before water falls off to the main sea color.
- Water should stay simple, clean, and saturated enough to frame the islands, not compete with them.
- Avoid detailed wave simulation, noisy normals, or physically correct water if they muddy the composition.

### Rendering and Lighting
- Aim for painterly clarity, not realism.
- Shading should support broad forms first: top planes, cliff planes, and shoreline separation.
- Use cheap, high-impact tricks before adding geometry complexity.
- Good candidates are soft ambient grounding, simple contact darkening, restrained outlines, and deliberate color separation between top, side, and coast.
- Lighting should remain soft and forgiving. Harsh realism will fight the diorama quality.
- If outlines are introduced, they should reinforce big shapes and grouped forms, not create visual fuzz.

### Set Dressing
- Dressing is a late-stage support system, not a foundation.
- Use few props: small rock stacks, occasional path cues, and minimal vegetation or markers only where they help scale and composition.
- Place props to reinforce edges, entries, look-at points, and height changes.
- Keep open ground readable. The terrain should still carry the scene if all props are removed.
- Avoid dense foliage, prop carpets, and biome clutter.

## Technical Translation
- The spline outline remains the source of truth for the top silhouette. Do not undermine that with downstream noise passes that redraw the island.
- Future beach, cliff, and shoreline decisions should be derived from boundary distance bands and shape rules, not hand-painted per-island exceptions.
- Irregularity should come from controlled segmentation, terracing, and band logic, not broad procedural noise layered everywhere.
- Prioritize material and shader layering before inventing complex mesh detail. A better top/side/coast read will outperform extra polygons.
- Coast classification should become a first-class system decision: beach edge versus cliff edge, with different material and shading responses.
- Decoration should remain a later pass and must never hide the landform read.
- When in doubt, choose the rule that preserves silhouette clarity from a distance.
- Borrow from Oskar-style methodology at the principle level:
  - systems-first aesthetics
  - controlled wonk instead of repetition or chaos
  - simple rendering tricks with strong payoff
  - strict scope discipline

## Phased Build Order
1. Silhouette and terrace profile.
   Lock the top-down read, island massing, and side-wall stepping before polishing anything else.
2. Beach band and cliff-band separation.
   Introduce readable land-to-coast material zoning and cliff identity.
3. Shoreline foam and shallow-water treatment.
   Make the land-water boundary attractive and unmistakable.
4. Material and shader polish.
   Improve painterly read through top/side/coast separation, soft grounding, and optional outlines.
5. Sparse rocks, paths, and minimal dressing.
   Add only enough detail to support scale and composition.

## Non-Goals
- Not photoreal terrain.
- Not erosion-first noise generation.
- Not dense foliage or ecosystem simulation.
- Not a direct Townscaper clone.
- Not a direct Bad North clone.
- Not a prop-heavy scene where terrain becomes background.

## Use This Document
- If a new feature improves realism but weakens silhouette, reject it.
- If a new system adds detail but does not improve grass, sand, stone, cliff, or shoreline read, defer it.
- If a prototype looks busy, first remove props and noise before adding more shading or geometry.
- If the work starts to drift, return to the pillars: silhouette, terraces, clean material hierarchy, graphic shoreline, sparse dressing.
