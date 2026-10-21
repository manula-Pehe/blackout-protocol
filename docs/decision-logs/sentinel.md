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

## Verification status and known gaps

The sandbox scenario runner checks patrol movement, detection and alarm publication, Alert-to-Track, loss of sight behind an occluder, Search, Return, and resuming patrol. The supplied Console captures show those scenario checks passing. This is sandbox evidence only; it does not establish that all edge cases pass or that the other agents react to the alarm in `Main`.

Repository inspection for this entry found no Sentinel UCS comparison implementation or dedicated diagnostic view in the current Sentinel scripts; the sandbox runner's OnGUI panel reports only scenario status and current state. No Sentinel-specific EditMode test files were found. `GameEvents.RaiseAlarm` directly invokes subscribers, with no cooldown in that method; alarm throttling has not been demonstrated. The brief calls for UCS comparison/debug evidence, alarm cooldown behavior, edge-case demonstrations, and a Main-scene run with the other agents. Those items still need separate verification or implementation; this log does not claim they are complete.
