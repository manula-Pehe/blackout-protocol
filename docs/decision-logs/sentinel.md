# Decision Log — Sentinel Drone / Ashen

## 2026-10-21 — Keep Sentinel decisions separate from Unity integration

**Context:** The Sentinel needs vision, navigation, patrol and search decisions while using the shared `IAgentBody` contract for physical movement.

**Options considered:**

1. Put perception, state transitions, route selection and flight movement in one Unity component.
2. Keep the state transition model in `SentinelHsm`, use `SentinelDroneBrain` as the Unity-facing adapter, and delegate movement to `SentinelDroneBody`.

**Decision:** Use the second arrangement. `SentinelHsm` owns the current state and transition rules. `SentinelDroneBrain` reads player visibility, selects paths and observation/search targets, raises the alarm, and sends `MoveCommand`s through the body. `SentinelDroneBody` implements `IAgentBody` and handles hover, movement, collision blocking, tilt and stuck reporting.

**Why:** The state transition model can be exercised without moving or rendering a Unity object, while Unity-specific perception and movement remain at the integration boundary.

**Evidence:** `Assets/_Project/Scripts/AI/Sentinel/SentinelHsm.cs`, `SentinelDroneBrain.cs`, and `SentinelDroneBody.cs`.

## 2026-10-21 — Build Sentinel routes from the baked NavMesh triangulation

**Context:** The assignment requires the Sentinel to use its own A* pathfinding over the baked NavMesh and send the resulting route to the shared agent body.

**Options considered:**

1. Delegate route planning and movement to Unity's `NavMeshAgent`.
2. Build a Sentinel-owned triangle-centroid graph, run A* over its links, and use NavMesh queries to sample endpoints and validate route shortcuts.

**Decision:** Use the Sentinel-owned graph and search in `SentinelNavMeshPathfinder`. Sample the start and goal onto the NavMesh, find a graph path, then tighten it only where `NavMesh.Raycast` confirms the shortcut stays on the walkable surface. Send the route to `SentinelDroneBody` via `MoveCommand`.

**Why:** This keeps the agent's path search explicit and inspectable while allowing Unity NavMesh queries to provide the baked walkable geometry.

**Evidence:** `Assets/_Project/Scripts/AI/Sentinel/SentinelNavMeshPathfinder.cs` and `SentinelDroneBrain.cs`.

## 2026-10-21 — Score patrol and observation destinations; search along the last heading

**Context:** A fixed waypoint loop and random search would not demonstrate the Sentinel's patrol selection, safe observation distance, or predictive search behavior.

**Decision:** Score reachable patrol points using time since last visit minus a route-length penalty. While tracking, score candidate positions around the player for preferred observation distance, route length and visibility, increasing the preferred distance when the player is aiming at the drone. When sight is lost, form a short ordered set of search points from the last known position and heading, then return to a patrol point after completing the search.

**Why:** These choices make the destinations reflect route reachability, recent patrol history, visibility, player direction and tactical distance.

**Evidence:** `Assets/_Project/Scripts/AI/Sentinel/SentinelDroneBrain.cs` and the Sentinel sandbox scenario logs supplied during project testing.

## 2026-10-21 — Compare A* and UCS on the same Sentinel graph query

**Context:** The assignment asks for a visible heuristic comparison that uses the same graph and endpoints, with node expansions and search time available for the viva.

**Decision:** Keep the production route on A*. Add an on-demand UCS run to `SentinelNavMeshPathfinder`, using the same NavMesh graph anchors and setting the heuristic to zero. Add an F1 debug view that reports the operational/composite/leaf state path, perception facts, current decision candidates and both searches' cost, expanded nodes and elapsed search time. Draw the vision cone and considered targets in Scene view, with A* and UCS graph paths in separate colors.

**Why:** The comparison is explicit and repeatable without making routine movement pay for two searches. The panel also exposes current decisions so the viva can show both the hierarchy and the planner's choices.

**Limitations:** Search costs omit the sampled endpoint-to-anchor legs. Timings measure only the graph search and are noisy for short runs; repeat the comparison and use the same machine/build conditions when discussing time.

**Evidence:** `Assets/_Project/Scripts/AI/Sentinel/SentinelNavMeshPathfinder.cs`, `SentinelDebugView.cs`, `SentinelDroneBrain.cs`, and `docs/evidence/core-developer/sentinel-ucs-debug.md`.

## Verification status and known gaps

The sandbox scenario runner checks patrol movement, detection and alarm publication, Alert-to-Track, loss of sight behind an occluder, Search, Return, and resuming patrol. The supplied Console captures show those scenario checks passing. This is sandbox evidence only; it does not establish that all edge cases pass or that the other agents react to the alarm in `Main`.

The A* vs UCS comparison and F1 debug view are implemented.

## 2026-10-21 — Isolate Sentinel logic for EditMode verification

**Context:** The assignment's unit-test checklist requires repeatable checks for HFSM transitions and child exits, patrol scoring, predictive search ranking, and A* versus UCS on identical graph data.

**Decision:** Keep the Unity-facing brain and NavMesh adapter thin by extracting patrol/search decision helpers and the fixed-graph search implementation into testable runtime logic. Add leaf-state exit/enter notifications so hierarchy transitions can be verified explicitly. Cover the four required areas in `Assets/_Project/Tests/EditMode/Sentinel/`.

**Verification:** Unity compiled the scripts without warnings. The nine Sentinel EditMode tests passed, including the HFSM parent change and child exit, patrol preference, heading prediction ranking, A*/UCS cost and expansion comparison, disconnected goal, and start-equals-goal cases. The full project EditMode run timed out, so no full-suite result is claimed.

**Evidence:** `Assets/_Project/Tests/EditMode/Sentinel/` and `docs/evidence/core-developer/sentinel-editmode-tests.md`.

![alt text](image.png)
