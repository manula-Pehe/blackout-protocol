# Decision Log — Agent Controller / Hunter

## 2026-10-19 — Use normalized explicit curves with hysteresis

**Context:**  
The Hunter must choose between Wander, Investigate, Chase, Search and Attack from noisy perception and memory inputs. The first branch needs deterministic, testable decision logic without runtime or scene dependencies.

**Options considered:**
1. Unity `AnimationCurve` assets — visually editable, but couples the foundation to UnityEngine and serialized assets.
2. A rule-based state machine — simple, but does not demonstrate Utility AI and creates hard thresholds that switch rapidly.
3. Explicit mathematical response curves — engine-independent, reviewable in code, deterministic and straightforward to unit test.

**Decision:**  
Use normalized scalar inputs, SmoothStep response curves, weighted multiplicative scores, and a configurable bonus for the current action. Resolve exact ties with the fixed priority Attack, Chase, Investigate, Search, then Wander.

**Why:**  
SmoothStep suppresses insignificant input noise while remaining responsive through the useful middle range. Multiplication makes trust and attack eligibility meaningful gates. The current-action bonus provides hysteresis without hiding a clearly superior action. Returning both base and adjusted scores makes future runtime debug displays explainable.

**Evidence:**  
EditMode tests cover all five selected actions, low-trust noise, hysteresis, deterministic ties and invalid numeric inputs.

---

## 2026-10-19 — Triangle-centroid navigation with Hunter-owned A* and UCS

**Context:**
The Hunter needs explainable paths over the baked NavMesh without delegating search or movement to `NavMeshAgent`. The Core `IPathfinder` contract requires world-space waypoints.

**Decision:**
Use one graph node per valid NavMesh triangle centroid. Two nodes connect only if their triangles share a complete edge (vertices matched to 1 mm in all three axes) and `NavMesh.Raycast` finds no obstruction between centroids. The edge cost is 3D Euclidean centroid distance. Reject non-finite nodes, invalid triangles and edges cheaper than their geometric distance. Non-manifold edges do not acquire extra links. Build once on first request; a future owner calls `InvalidateGraph()` after rebaking or changing the NavMesh.

A* uses `h(n) = 3D distance(n, goal)` and `f(n) = g(n) + h(n)`. This is admissible and consistent while every nonnegative edge cost is at least the same geometric distance. UCS runs identical search with `h = 0`. A binary min-heap orders by `f`, then `h`, node ID and insertion order. Stale entries are discarded; equal-cost routes keep the first discovered parent. Metrics expose graph-route cost, nodes expanded and elapsed milliseconds. Elapsed time is diagnostic and not deterministic. The world route adds sampled start/goal around the graph centroids; graph cost excludes those short endpoint legs.

**Why:**
The search and triangulation-to-graph conversion remain pure C# in the Hunter logic assembly. Only `HunterNavMeshPathfinder` touches Unity's NavMesh API and implements Core `IPathfinder`. This differs from Breacher's linear open list and per-search graph build. It also allows EditMode tests to compare A* and UCS on exactly the same weighted graph.

**Evidence:**
Unity 6000.3.25f1 EditMode test run: 69/69 passed, including 10 new Hunter navigation tests and the pre-existing Hunter utility and sound-memory tests. The runtime adapter has no baked-scene integration test yet.

**Limitations:**
Triangle-center paths are valid but not necessarily shortest world-space routes; there is no funnel smoothing. The nearest visible centroid anchors may not be ideal on overlapping/stacked NavMeshes. The adapter needs an explicit invalidation after rebakes and does not respond to dynamic obstacles unless the baked navigation data changes. A search limit can return failure on a very large graph. No movement, brain, sound propagation or prefab integration is included.
Endpoint anchoring scans graph nodes linearly, and off-mesh links are not represented.

---

## 2026-10-19 — Bounded sound memory with location-specific impact habituation

**Context:**
The Hunter needs to remember sounds, prefer useful recent evidence, and become less responsive to repeated distractions without ignoring gunshots or alarms.

**Options considered:**
1. Store every event and filter at query time — simple, but memory grows indefinitely.
2. Reduce trust for every sound category after repeated impacts — easy, but a decoy could suppress genuine combat or an alarm.
3. Keep fixed-size sound and impact-location arrays — bounded work and storage, with targeted habituation.

**Decision:**
Store at most 16 observations by default (hard maximum 64). Use the existing `NoiseType` and `NoiseLevels` contracts. The caller converts `NoiseEvent.Position` to three numeric coordinates and supplies its timestamp, loudness in metres, and an initial perception confidence. No Unity types enter the memory assembly. Expire observations after 30 seconds or once confidence falls below 0.01.

For elapsed time `t`, confidence is `initialConfidence × 2^(-t / 6 seconds)`. Recency is `1 / (1 + t / 6 seconds)`. Loudness intensity is `min(1, loudness / category reference loudness)`, using running footsteps as the footstep reference. Priority is `intensity × confidence × recency × effectiveTrust × categoryWeight`, with weights Alarm 1, Gunshot 0.95, Door 0.7, Footstep 0.6, and Impact 0.55. These weights make a fresh credible alarm or gunshot more urgent than a comparable impact while preserving the caller's loudness and confidence effects.

Only impacts habituate. A repeat within 2 metres of a fixed location and 8 seconds adds 0.2 penalty after the previous penalty decays; penalty is capped at 0.8. Keeping each location fixed prevents a moving chain of impacts from spreading the penalty across a room. Between impacts, `penalty(t) = penaltyAtLastImpact × 2^(-t / 20 seconds)`, and effective trust is `1 - penalty(t)`. A new location starts at full trust. Gunshots, footsteps, doors, and alarms retain trust 1.

**Why:**
Fixed arrays avoid per-record and per-query allocations. Explicit caller time makes decay deterministic in EditMode tests. The returned observation includes confidence, intensity, recency, effective trust and priority so a later brain can feed selected sound intensity, memory confidence and trust into `HunterUtilityContext` without changing the selector. Sound propagation, world sensing and decision execution remain separate work.

**Evidence:**
Hunter EditMode tests check decay, priority, expiration, spatial habituation, recovery, category isolation, invalid inputs and capacity.

---
