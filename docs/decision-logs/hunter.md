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

## 2026-10-19 — Stage A: visible navigation sandbox

**Decision:**
Create `Assets/_Project/Scenes/Sandbox/Manula/HunterSandbox.unity` with a primitive Hunter, a floor, and a wall that blocks the direct start-to-goal route. The robot uses the existing CharacterController, AgentMotor, AgentRotation and AgentBody. A Hunter-owned one-route test driver calls HunterNavMeshPathfinder and submits the resulting waypoints through `IAgentBody.Execute(MoveCommand)`. It reports arrival, stuck conditions, missing references, missing NavMesh data and failed path searches, and displays the calculated route and search metrics.

The NavMeshSurface collects only its floor/wall children using physics colliders. The bake uses the existing Humanoid agent settings, with 0.1 m voxels. The controller radius is 0.3 m; intermediate waypoint acceptance is 0.12 m to leave clearance inside the baked agent radius. Persist the baked data as `HunterSandboxAssets/HunterSandboxNavMesh.asset` and serialize that asset reference on the scene's surface, matching the repository's standalone NavMeshData convention. Playing the scene does not bake navigation data. `Tools > Hunter > Bake Open Hunter Sandbox` updates the existing data asset while preserving its GUID after geometry edits.

**Assembly boundary:**
Add a Hunter Runtime assembly referencing the existing Hunter Logic, Core, Body and AI Navigation assemblies, plus an Editor assembly for the scene-creation/bake tools. The pure logic assembly remains engine-independent. The Hunter test assembly can now test runtime integration as well as the earlier pure features.

**Verification design:**
The sandbox integration test closes and reopens the saved scene, enters Unity Play Mode, confirms that the direct NavMesh ray is blocked, observes the robot moving beyond the obstacle tip, and requires AgentBody arrival within 0.2 m of the goal. A second Play Mode test checks missing references, missing bake data and an unreachable goal. Neither runtime test introduces HunterBrain, utility decisions or sound processing.

**Executed evidence:**
Unity 6000.3.25f1 ran the complete EditMode suite after restarting the Editor from the bake process: 71/71 passed, including both sandbox tests which explicitly enter Play Mode. The Hunter reached `(6.09, 0.03, -2.92)` near the `(6, 0, -3)` goal, with maximum observed Z `5.677` beyond the wall tip at Z `2`. The calculated route had 14 waypoints on an 18-node graph. The diagnostic errors in the second test were intentional and matched with `LogAssert.Expect`.
The saved scene's camera was also rendered in a graphics-enabled Unity Editor process and visually inspected: robot, obstacle, goal and calculated route are visible. Open the saved scene and press Play; no initial bake or reference assignment is required. To rerun the automated checks, use Window > General > Test Runner > EditMode > Run All (the sandbox tests enter and exit Play Mode themselves).

---

## 2026-10-19 — Stage B: autonomous sound response

**Decision:**
`HunterBrain` coordinates the existing selector, bounded sound memory, Hunter A* adapter and `IAgentBody`. It subscribes to `GameEvents.Noise`, `AgentBody.Arrived` and `AgentBody.Stuck` on enable and removes those listeners on disable. It stops the body when relinquishing control. The Stage A route driver refuses to run while an enabled brain owns the same body.

The runtime evaluates every 0.2 seconds and limits A* requests to one per 0.75 seconds. An unchanged active destination reuses its path; a moving target must move at least 0.8 m before a new route is requested. A stuck event stops movement, invalidates the cached graph, and schedules a delayed replan; repeated stuck events abandon that pursuit for a bounded search. Failed routes are consumed as resolved evidence and also have a retry cooldown.

**Perception and Utility inputs:**
The shared loudness is an audible radius in metres. Reject invalid, future, self-generated and out-of-range observations. For a sound at distance `d` inside audible radius `r`, initial confidence is `sqrt(1 - d/r)`. Pass the original category, radius and timestamp into `HunterSoundMemory`. Its existing decay and habituation formulas remain authoritative. Runtime memory uses configurable half-life 24 s and maximum age 60 s so a one-shot cue lasts long enough for the sandbox detour; this does not change the pure memory defaults.

Utility sound intensity is selected memory intensity multiplied by its current confidence. Confidence and trust come directly from memory. Proximity is `clamp01(1 - distance/range)`, with 24 m for remembered sounds and 12 m for visible players. Search urgency is 1 during a bounded search. A visible player contributes confidence/trust 1 without inventing a noise. Vision uses the registered live player, a 12 m radius and a physics line-of-sight check. There is no acoustic wall attenuation or field-of-view cone in this stage.

**Actions:**
- Wander: visit the sandbox's deterministic floor points at Walk speed, with a short pause on arrival. A small deterministic sampled route around the starting location is the fallback when no points are assigned.
- Investigate: Walk along a Hunter A* path to the strongest unresolved trusted sound.
- Chase: Run toward a visible player or a strong nearby trusted sound. A sound alone never authorizes an attack.
- Search: sample nearby positions around the last trail at Sneak speed for at most 8 s. Reaching a sound, losing sight, insufficient confidence, or failed pursuit starts this phase. Resolving the observation timestamp prevents repeatedly rediscovering the same trail. A backwards-compatible filtered memory query lets a newer quieter sound interrupt a search even while an older resolved alarm remains in memory.
- Attack: require a registered, active, alive player with valid health/position, clear physics line of sight and actual 3D distance at most 1.4 m. Recheck at execution, stop movement, dispatch `AgentAction.Attack`, and optionally apply 10 damage through existing `IDamageable`, with a 1.2 s cooldown. No animation implementation is added.

**Sandbox:**
Open `Assets/_Project/Scenes/Sandbox/Manula/HunterNoiseSandbox.unity`. It reuses Stage A's floor, wall, camera, materials and persisted NavMeshData; the original `HunterSandbox.unity` remains the navigation-only demo. Press Play to wander. Use Game view buttons: **Alarm across wall** for investigation and the detour, **Gunshot nearby** for a strong Chase cue, **Repeated impacts** for habituation, and **Show target / Hide target** for the stationary registered player stand-in. The latter controls do not generate sounds or directly command the brain. Allow a one-shot cue to cool or be reached to observe Search and then Wander. The on-screen display reports action, selected category/position, confidence, trust and priority. A cyan route and waypoint Gizmos show the actual A* route.

**Limits:**
Static baked navigation, triangle-centroid waypoints and the shared body remain unchanged. There is no advanced acoustics, automatic NavMesh rebake, animation system, moving test player, or new shared contract. Observations sharing exactly the same timestamp are resolved as a batch. The scene shares Stage A's NavMesh asset and geometry; rebaking changed Stage B geometry independently would need a separate bake asset. The target is a sandbox stand-in, not a replacement player controller.

**Executed verification:**
The first complete run retained all 71 existing passes but failed the four new runtime tests because captured coroutine locals did not survive `EnterPlayMode` domain reload. Runtime assertions were moved into coroutines created after entering Play Mode; focused diagnostics established that the scene and brain were present throughout. The corrected new fixture passed 5/5 (four runtime integrations and the resolved-memory regression). The subsequent complete Unity 6000.3.25f1 EditMode suite, run with graphics enabled, passed 76/76, including the unchanged Stage A tests. The autonomous detour test observed arrival at `(6.09, 0.03, -2.92)`, maximum Z `5.68` beyond the wall tip, and one successful injected stuck-event recovery.
The focused graphics-enabled visual run also passed 5/5. Live camera images for Wander, Investigate, Chase, Search and Attack were captured and inspected. These camera-only diagnostic images omit IMGUI overlays; the action/noise display and buttons are drawn by `OnGUI` in the normal Game view. Temporary capture tools and result XML files stayed in the isolated verification project.

**Stage B file inventory (relative to repository root):**
```text
Assets/_Project/Scripts/AI/Hunter/HunterBrain.cs
Assets/_Project/Scripts/AI/Hunter/HunterBrain.cs.meta
Assets/_Project/Scripts/AI/Hunter/Logic/HunterSoundMemory.cs
Assets/_Project/Scripts/AI/Hunter/Testing/HunterSandboxRouteDriver.cs
Assets/_Project/Scripts/AI/Hunter/Testing/HunterSandboxNoiseTrigger.cs
Assets/_Project/Scripts/AI/Hunter/Testing/HunterSandboxNoiseTrigger.cs.meta
Assets/_Project/Scripts/AI/Hunter/Testing/HunterSandboxTarget.cs
Assets/_Project/Scripts/AI/Hunter/Testing/HunterSandboxTarget.cs.meta
Assets/_Project/Scripts/AI/Hunter/Editor/HunterNoiseSandboxSetup.cs
Assets/_Project/Scripts/AI/Hunter/Editor/HunterNoiseSandboxSetup.cs.meta
Assets/_Project/Tests/EditMode/Hunter/HunterBrainTests.cs
Assets/_Project/Tests/EditMode/Hunter/HunterBrainTests.cs.meta
Assets/_Project/Scenes/Sandbox/Manula/HunterNoiseSandbox.unity
Assets/_Project/Scenes/Sandbox/Manula/HunterNoiseSandbox.unity.meta
docs/decision-logs/hunter.md
```

---

## 2026-10-19 — Stage C: debugging and robustness

**Decision snapshots, not a second AI:**
`HunterDebugPanel` copies `HunterBrain.LastSelection` (all five base and hysteresis-adjusted scores), its decision timestamp/context, and the selected `HunterRememberedNoise`. It never invokes the selector, memory query or perception to draw the panel. Sound age is measured at that same decision timestamp, so confidence, trust, priority and age refer to one consistent snapshot. Empty/expired memory is explicitly labelled. The selected action is marked in the score rows and in the persistent compact action header.

`HunterTransitionHistory` is a fixed-capacity ring (12 entries in the brain), newest first. Only actual action changes are recorded, together with the exact selection, previous action, time and reason. Reasons identify trusted observations, visible/lost targets, trail cooling, expiration, arrival, failed pursuit, repeated stuck events and completed search. The display includes the previous action's adjusted score and the winning adjusted/base scores; explanations do not claim that every change was caused by a new sound.

**Navigation assessment evidence:**
The pathfinder stores the actual A* result, cached graph, endpoint anchor IDs and expansion budget for each query. `TryCompareLastSearch` runs UCS on those same anchors, graph and budget at most once per query. It neither reruns A* nor rebuilds the graph. A new request or invalidation clears stale comparison data, including unsampleable endpoints; a failed search is labelled FAILED rather than showing a misleading zero cost. Graph cost excludes the sampled endpoint-to-anchor legs. Elapsed milliseconds measure the search only and are diagnostic samples, not controlled performance benchmarks.

The panel exposes destination, waypoint count, movement status, path requests/failures, stuck recoveries, graph/search counts, both search results and their absolute cost difference. Normal components default to manual comparison. The autonomous sandbox enables comparison on route requests for assessment. **Compare latest route** also works with automatic comparison off and reuses a cached comparison on repeat clicks. F1 hides/shows the detailed panel, disables automatic UCS while hidden, and retains the compact action label and essential noise/target buttons. This project uses the new Input System, so the Hunter runtime/test assemblies reference the existing `Unity.InputSystem` assembly; no package or input project setting is changed.

**Robustness:**
Repeated stuck callbacks are coalesced during the repath interval (default 0.75 s). The first accepted event invalidates the graph for a deferred rebuild; the second failed pursuit starts a bounded local search rather than retrying the same obstructed destination immediately. A failed pursuit location is excluded for the search duration (at least 1 s), unless the target moves outside the cooldown radius. Unreachable sound timestamps are resolved and local search is centred on the Hunter's current position. Invalid/destroyed registered Unity targets are rejected before transform access, and Attack still rechecks validity, visibility and actual range. Missing/disabled movement references stop movement with a diagnostic status.

**Performance and boundaries:**
Visible text is cached at 4 Hz using a reusable StringBuilder; hidden mode updates only the compact header. There are bounded history entries, no normal-operation console spam, no per-frame UCS and no per-frame graph building. Formatted text and route/search results still allocate at their scheduled refresh/request boundaries. Selector formulas, memory decay/habituation, Core contracts, shared AgentBody, Stage A scene/NavMesh and Stage B movement architecture are unchanged. Static baked navigation and triangle-centroid routes remain limitations; there is no acoustic propagation, door attenuation, animation or Stage C graphics work.

**Use and testing:**
Open `Assets/_Project/Scenes/Sandbox/Manula/HunterNoiseSandbox.unity`, press Play and focus the Game view. Use the existing alarm/gunshot/impact/target controls. Scroll the F1 panel for utility, memory, navigation, A*/UCS and recent transitions. The original `HunterSandbox.unity` remains the navigation-only demonstration. If regenerating the autonomous sandbox, its creation tool adds the panel; `Tools > Hunter > Enable Debug Panel in Noise Sandbox` can attach/configure it without rebuilding the existing scene or rebaking navigation. Run Window > General > Test Runner > EditMode > Run All; the Hunter runtime integrations explicitly enter/exit Play Mode.

**Executed verification:**
Unity 6000.3.25f1 executed the complete EditMode suite in an isolated copy with identical Hunter scripts, tests and saved sandbox scenes: **81/81 passed, zero skipped**. This includes the previous 76 tests and five Stage C tests: bounded transition history; score snapshot equality/cached comparisons/F1 input; expiration and finite empty-state scores; repeated-stuck/unreachable-route recovery without graph-build spam; and a deliberately stale destroyed registered attack target. The runtime integration cases enter/exit real Play Mode. Existing Stage A navigation and Stage B autonomy tests remain passing. Result XML: `/tmp/hunter-stage-a.weTEGo/stage-c-all.xml`; Editor log: `/tmp/hunter-stage-a.weTEGo/stage-c-all.log`.

Initial focused runs exposed two test-harness issues, not ignored failures: a score assertion before the next scheduled decision, and synthetic input being routed away from the headless Game view. Assertions now poll the scheduled snapshot, and the F1 test processes queued keyboard events with explicit temporary focus/update settings, restored in `finally`. The final focused F1 test and subsequent full suite both passed. `git diff --check` also passed. No commits or branch operations were performed.

A subsequent graphics-enabled Editor run entered Play Mode and emitted an alarm: the live debug snapshot selected Investigate (base 0.690, adjusted 0.770), with alarm confidence 0.788, trust 1.000 and priority 0.689. Cached A* and UCS both cost 29.094 and expanded 13 nodes on anchors 1 -> 4; one graph build served both route requests. Initial desktop screen-read captures were black. On **2026-10-20**, Unity's direct Game-view capture successfully included IMGUI and was visually inspected: all five score rows and the selected action, sound category/age/confidence/trust/priority, navigation diagnostics, A*/UCS comparison, and transition explanation are readable. The robot, wall and cyan detour route are visible, with all five essential sandbox buttons unobstructed below the panel. That capture selected Investigate (base 0.692, adjusted 0.772), with identical A*/UCS costs of 29.094. The remaining visual-verification gap is therefore closed. Long-history scrolling is available; the F1 keyboard path and retained controls are covered by the runtime test.

![Live Hunter Stage C Game view: scores, sound memory, transitions, navigation comparison and preserved sandbox controls](hunter-stage-c-gameview.png)

The separate visual runs logged an Editor QuickSearch `SearchDatabase.EnumerateAll` indexing exception outside Hunter code; the completed 81-test run was unaffected. Capture helpers and logs remain in the temporary verification project, not Unity project assets. The screenshot above is retained as Hunter assessment documentation only.

After the successful visual capture, the complete Unity EditMode suite was rerun on **2026-10-20**: **81/81 passed, zero failures, zero skipped**, including the Hunter runtime integrations. Final result XML: `/tmp/hunter-stage-a.weTEGo/stage-c-completion.xml`; log: `/tmp/hunter-stage-a.weTEGo/stage-c-completion.log`. Hunter source/test directories and both saved sandbox scenes match the tested copy. This completion follow-up changed only this log and added its screenshot; no runtime code, scenes, shared files, packages or settings were changed further.

**Stage C changed-file inventory (relative to repository root; earlier Stage A/B working-tree files are preserved):**
```text
Assets/_Project/Scripts/AI/Hunter/HunterDebugPanel.cs
Assets/_Project/Scripts/AI/Hunter/HunterDebugPanel.cs.meta
Assets/_Project/Scripts/AI/Hunter/Logic/HunterDebugHistory.cs
Assets/_Project/Scripts/AI/Hunter/Logic/HunterDebugHistory.cs.meta
Assets/_Project/Scripts/AI/Hunter/HunterBrain.cs
Assets/_Project/Scripts/AI/Hunter/HunterNavMeshPathfinder.cs
Assets/_Project/Scripts/AI/Hunter/BlackoutProtocol.Hunter.Runtime.asmdef
Assets/_Project/Scripts/AI/Hunter/Editor/HunterNoiseSandboxSetup.cs
Assets/_Project/Tests/EditMode/Hunter/HunterDebugTests.cs
Assets/_Project/Tests/EditMode/Hunter/HunterDebugTests.cs.meta
Assets/_Project/Tests/EditMode/Hunter/BlackoutProtocol.Hunter.Tests.EditMode.asmdef
Assets/_Project/Scenes/Sandbox/Manula/HunterNoiseSandbox.unity
docs/decision-logs/hunter.md
docs/decision-logs/hunter-stage-c-gameview.png
```
