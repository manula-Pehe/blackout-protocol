# Decision Log — Systems Engineer / Breacher

## 2026-10-09 — Use a custom graph search over NavMesh geometry

**Context:**  
The Breacher needs to navigate the Unity level using an individually implemented search algorithm. Using `NavMeshAgent` for the complete route calculation would hide the required search work.

**Options considered:**

1. Use `NavMeshAgent` for pathfinding and movement.
2. Build a manual waypoint graph for the test level.
3. Convert Unity's baked NavMesh triangulation into a graph and run a custom search over it.

**Decision:**  
Use Unity's baked NavMesh only as a source of walkable geometry. Convert its triangles into graph nodes and connections, then search that graph with the custom A* implementation.

**Why:**  
This keeps level authoring practical while ensuring the route selection logic is our own. It also lets the graph update when the NavMesh is rebaked instead of requiring hand-maintained waypoints.

**Evidence:**  
The sandbox graph contained 24 nodes. For the tested start and goal, A* and Uniform Cost Search both returned cost `14.50`, while A* expanded 5 nodes and UCS expanded 6. The matching optimal costs verify the A* result on this case. Timing on this small graph is not treated as meaningful performance evidence.

## 2026-10-09 — Use straight-line distance as the A* heuristic

**Context:**  
A* needs a heuristic that guides search without overestimating the remaining route cost.

**Options considered:**

1. Use no heuristic, making the search equivalent to Uniform Cost Search.
2. Use straight-line distance between the current node and goal.
3. Multiply straight-line distance to make the search greedier.

**Decision:**  
Use unscaled Euclidean distance to the goal.

**Why:**  
Graph edge costs are based on Euclidean distance, so the direct distance cannot exceed the cost of a multi-edge route between the same positions. This makes the heuristic admissible for the current graph cost model. Scaling it upward could lose the optimality guarantee.

## 2026-10-11 — Represent GOAP world state as an immutable bitmask

**Context:**  
The planner repeatedly compares, copies, and hashes world states while exploring possible action sequences.

**Options considered:**

1. Store facts in a dictionary of strings and booleans.
2. Store facts in mutable collections.
3. Store boolean facts in an immutable bitmask value.

**Decision:**  
Use an immutable bitmask-based `WorldState`.

**Why:**  
It provides inexpensive copies, equality checks, and hashing, while avoiding accidental mutation of states already stored in the planner's search structures. The fixed fact definitions also reduce errors caused by misspelled string keys.

## 2026-10-11 — Use GOAP for Breacher high-level decisions

**Context:**  
The Breacher must choose multi-step behaviour when its direct route is obstructed, including opening a usable door or breaching a barricade.

**Options considered:**

1. Implement a fixed finite-state machine with hard-coded transitions.
2. Implement a behaviour tree with fixed branches.
3. Use Goal-Oriented Action Planning with action preconditions, effects, and costs.

**Decision:**  
Use GOAP for high-level planning.

**Why:**  
GOAP can assemble a sequence from the current facts and reconsider the route when conditions change. Action costs allow the same system to prefer opening a door normally, then select the barricade route when the door route becomes unavailable or more expensive. It is also straightforward to add new actions without rewriting a large transition structure.

**Evidence:**  
The debug scenario produces `Move To Door -> Open Door -> Move To Player -> Attack`. Increasing the door-route cost causes the planner to select the barricade alternative.

## 2026-10-11 — Separate planning from action execution and replanning

**Context:**  
A valid plan can become invalid while it is running because an action fails, a precondition disappears, the level changes, the agent becomes stuck, the player moves, or the goal changes.

**Options considered:**

1. Execute the entire plan without checking the world again.
2. Put execution and scene-specific movement inside the planner.
3. Keep the planner pure and use a separate executor that reports when replanning is required.

**Decision:**  
Use a separate `PlanExecutor` with an `IGoapActionRunner` boundary and explicit replanning reasons.

**Why:**  
The pure planner remains deterministic and easy to test, while Unity-specific actions can be connected later through runners. The executor verifies preconditions before each action and requests replanning instead of continuing with a stale sequence.

**Evidence:**  
All 18 Breacher EditMode tests pass, including ordered execution, action failure, lost preconditions, level changes, empty plans, planner behaviour, world-state behaviour, and concrete action scenarios.

## 2026-10-11 — Bound the planner search

**Context:**  
An unreachable goal or an incorrectly modelled action set must not allow the planner to explore indefinitely during gameplay.

**Options considered:**

1. Search until a plan is found or all states are exhausted.
2. Apply a maximum expansion limit and return a clear failure result.

**Decision:**  
Bound the number of states expanded by the planner, with a default limit of 1024.

**Why:**  
The limit gives predictable worst-case work and makes modelling failures visible to the caller, which can retain a safe behaviour or retry after the world state changes.

## 2026-10-11 — Smooth the triangle-centre path after A*

**Context:**  
The NavMesh graph uses triangle centres as nodes. Following every centre produced a valid route but caused visible left-right zig-zag movement.

**Options considered:**

1. Follow every graph node exactly.
2. Replace the custom search with `NavMeshAgent` pathfinding.
3. Keep the custom A* route and remove unnecessary intermediate waypoints with NavMesh line-of-sight checks.

**Decision:**  
Run a line-of-sight smoothing pass over the A* result before movement.

**Why:**  
The custom A* still determines the valid route and provides the search evidence. Smoothing only skips graph nodes when a direct segment remains on walkable NavMesh, producing more natural movement without allowing shortcuts through walls.

**Evidence:**  
The debug output reports both the original graph-node count and the reduced smoothed-waypoint count, while cyan Gizmos show the resulting straightened route.

## 2026-10-11 — Use runtime A* path lengths as GOAP movement costs

**Context:**  
Fixed movement costs made GOAP choices independent of the Breacher's actual location.

**Options considered:**

1. Keep designer-entered fixed movement costs.
2. Use straight-line distance only.
3. Query the custom A* without starting movement and use the returned route length.

**Decision:**  
Use custom A* route lengths for runtime movement-action costs, with Inspector values retained only as fallbacks when a route cannot currently be calculated.

**Why:**  
This lets GOAP choose between the door and barricade using the real navigable distance from the Breacher, including wall layout, rather than Euclidean distance or a hard-coded preference.

**Evidence:**  
Moving the Breacher closer to the door or barricade changed the displayed movement costs and selected plan. With equal interaction costs, the planner preferred the route with the cheaper A* approach.

## 2026-10-11 — Remove failed obstacle options before replanning

**Context:**  
A barricade pushed against a wall must not leave the executor running forever or cause it to choose the same impossible action repeatedly.

**Decision:**  
Detect a blocked kinematic push with a Rigidbody sweep, fail the running action, temporarily mark that barricade unavailable, and request a new plan. If no route remains, stop and wait safely rather than continuously replanning.

**Evidence:**  
With the barricade blocked, the Breacher changed to the more expensive door route. With both the barricade blocked and the door locked, it reported no valid plan once and remained responsive without log spam.

## 2026-10-18 — Separate the Breacher brain from shared movement and world objects

**Context:**  
The sandbox prototype initially moved its Transform directly and referenced concrete door, barricade, and player-health components. The merged shared contracts provide stable boundaries for final integration.

**Decision:**  
Make the Breacher path component implement `IPathfinder`; send its smoothed custom A* waypoints to `IAgentBody` using `MoveCommand`; interact with the world through `IDoor`, `IPushable`, `IDamageable`, and `GameEvents.LevelChanged`.

**Why:**  
GOAP remains responsible only for decisions, the custom A* remains responsible for route selection, and the shared body owns collision-aware movement, gravity, turning, and animation requests. Other implementations can replace doors, pushables, health, or the body without changing the planner.

**Evidence:**  
The integrated Breacher successfully completed both the unlocked-door route and the locked-door barricade route. Door and barricade completion raised `LevelChanged`, Player movement caused replanning, and repeated `IDamageable` attacks stopped when the Player reported `IsDead`.
