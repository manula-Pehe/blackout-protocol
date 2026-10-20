# Decision Log — GV / S4 Agent Controller

## 2026-10-20 — Increment 1: steering foundation and dependency audit

### Repository baseline and scope

Verified `feature/agentbody-steering` before editing. The only pre-existing worktree item was untracked `ProjectSettings/TimelineSettings.asset`; it is preserved. Unity is 6000.3.25f1; manifest declares URP 17.3.0, AI Navigation 2.0.14, Input System 1.20.0 and Test Framework 1.6.0. No packages/settings, shared contracts, brains, teammate prefabs, Main scene or navigation data are changed.

This is ground-body arrival/corner steering only: no crowd avoidance, look-ahead smoothing, pathfinding, dynamic rebake, animation blending, root-motion movement or flying-controller changes.

### Dependency map (inspected before implementation)

Status names distinguish source compatibility from actual runtime validation. A component existing does not imply that a saved prefab or Main scene is configured correctly.

| Agent/system | Actual request → movement path | Styles, events and orientation | Status and required follow-up |
|---|---|---|---|
| Hunter | `HunterBrain` → `HunterNavMeshPathfinder : IPathfinder` → ordered world-space `List<Vector3>` → `IAgentBody.Execute(MoveCommand)` on concrete `AgentBody` → `AgentMotor`/CharacterController | Wander/Investigate Walk; Chase Run; Search Sneak; Attack stops. No Hunter Strafe commands. Arrived starts/resumes search/wander; Stuck throttles replanning and can invalidate the cached graph; Stop on control relinquishment/failure/action change. Ground rotation faces travel direction. | **Available and compatible in architecture**; existing saved navigation/autonomous sandbox integrations are the regression checks. Hunter A* returns triangle-centroid nodes plus sampled endpoint legs. Tight bends must be preserved. No brain changes required. |
| Breacher | `BreacherAgent : IGoapActionRunner` obtains concrete `AgentBody` as `IAgentBody`. `BreacherPathMover : IPathfinder` builds its own centroid graph/A*, optionally smooths with NavMesh raycasts, and returns world-space start + route + goal. GOAP Begin sends Run for MoveTo/TakeCover. | GOAP Tick **polls HasArrived**, not the Arrived event, then IsMoving to distinguish running/failure. Subscribes Stuck to notify executor; Cancel and death call Stop. Optional player look target on Run does not mean Strafe: ground body normally faces travel while moving. Attack dispatches `PlayAction`. | **Available; scoped movement and existing scene startup validated separately below**. `BreacherPathMover` also has a legacy `MoveTo`/Update loop writing transform directly; it must remain idle while AgentBody owns movement. Standalone prefab serializes no ground body and has null scene targets. **PlayerControllerSandbox instantiates that prefab** and overrides all six targets, removes the old collider and adds AgentBody/Rotation/Motor/CharacterController. Raw YAML shows no BreacherHealth, but actual Unity loading **supplies the required component**: it exists before Play and after Start; the brain is enabled, shared body moving and legacy mover idle. Therefore health is not an observed blocker. Prefab-only spawning and the complete door/barricade/health gameplay sequence still require Chethiya's validation. |
| Flanker | No Flanker scripts, Behaviour Tree implementation or named Flanker assets found under Assets. Core `CoverPoint` exists (registry, position, forward-facing direction, occupancy), but is not evidence of Flanker integration. | Waypoint format, requested styles, event handling, animation and rotation cannot be confirmed. | **Missing/not yet integrated; blocked by Mahima's work** for a real Flanker regression. Supply ordered world-space points and confirm IAgentBody use; do not assume an adapter exists. |
| Sentinel | `SentinelDroneBrain` HSM → Sentinel-owned `SentinelNavMeshPathfinder : IPathfinder` → ground-space world-waypoint list → `SentinelDroneBody : IAgentBody`. Brain explicitly gets the flying body, **not** AgentBody. | Patrol Walk (2.2 m/s), search Sneak (1.5), combat Strafe (Walk speed) with player look target; body also supports Run 4.2. Brain polls HasArrived, subscribes Stuck and stops on disable/path failure. Flying body raises Arrived once, copies paths, adds 2.2 m hover/bob, sphere-casts for collisions and owns yaw/tilt. Rebuilds its pathfinder on LevelChanged. | **Available, separate flying implementation**. Ground steering changes do not reference or modify it. No substitution with CharacterController/AgentBody. Source-isolation check is not a live flight regression; Ashen owns flight/model acceptance. |
| Main scene / NavMesh | `Assets/_Project/Scenes/Main.unity` currently contains Directional Light and Main Camera only. Serialized built-in NavMesh data is null; no NavMeshSurface, enemy spawns, cover placement or route markers found there. | Persisted NavMeshSurface data exists in Hunter sandboxes, PlayerControllerSandbox and Sentinel_TestingGround. Main coverage/connectivity cannot be inferred from those separate bakes. | **Missing/not integrated; blocked by World Builder configuration** for final level acceptance. Mahima must supply Main geometry, persisted bake, connected routes and spawn/cover/waypoint placement. Sandbox verification does not establish Main readiness. |
| Doors / barricades / player physics | `SlidingDoor` moves panel Rigidbody and raises LevelChanged when open/closed state completes. `BreacherBarricade` sweeps/moves a kinematic Rigidbody and raises LevelChanged after clearing. `PushableObject` uses a dynamic Rigidbody, optional stationary carving, and settling notifications. Player FirstPersonController uses CharacterController and pushes dynamic bodies via OnControllerColliderHit. | Shared ground motor uses CharacterController collision; no warp, navigation shortcuts or automatic pushing are added. Breacher subscribes LevelChanged for GOAP replan; Sentinel rebuilds its graph. Hunter currently invalidates after stuck/manual request, not on every LevelChanged. | **Available but requiring joint dynamic-route validation**. Collision stops the body; it does not open a door, push a barricade or update a cached graph. Chethiya owns obstacle states, carving and action timing; Hunter cache/event policy is a later Hunter-owned handoff. No new Core interface required. |
| Models / animation / hit/death | SentinelDrone FBX and prefab are present; import settings have an empty explicit clip list. No standalone `.anim` or `.controller` assets were found. Ground `AgentAnimatorDriver` discovers an Animator child, disables root motion and writes actual horizontal `Velocity.magnitude` to float `Speed` with 0.1 s damping. | Driver currently does **not** subscribe to ActionRequested despite the body's event comment; no one-shot state mapping or model yaw correction exists in the ground body. Sentinel creates its own visual tilt pivot and (-90,0,0) axis correction. `BreacherHealth` death causes BreacherAgent.Stop/disable; PlayerHealth provides damage state. No generic ground-body enemy death/hit adapter was found. | Ground animation assets/integration **missing or requiring validation**; Ashen must confirm ground model +Z forward, rig, in-place clips/Speed controller and action/hit/death ownership. Do not claim empty importer clip list proves no embedded FBX animation; inspect subassets with the artist. Animation work does not block primitive steering. |

Relevant inspected sources are under `Assets/_Project/Scripts/AgentBody`, `Core/Movement`, `AI/Hunter`, `AI/Breacher`, `AI/Sentinel`, `Player` and `Interaction`. Saved prefab references and scene YAML were inspected in addition to code; no absent component was invented.

### Steering decision and formulas

Use the **supplied polyline**, never a spline, projected later segment or destination shortcut. Arrival desired speed is:

`v_arrive = v_style * clamp01(remaining_polyline_distance / slowingRadius)`.

At a bend (more than 5 degrees, or the first approach point), use a tighter radius:

`r_corner = min(waypointAcceptanceRadius, cornerAcceptanceRadius, 0.25 * adjacent_nonzero_segment_lengths)`.

Consecutive exactly duplicate points are collapsed so they cannot disguise a bend; non-finite or unrepresentably large points reject the entire route rather than bridging across an invalid waypoint. Straight intermediate points keep the existing acceptance tolerance. There is no path projection/look-ahead or line-of-sight shortcut. World-space Y remains ignored for ground waypoint distances, as before.

Braking uses `v² = terminal² + 2*a*d`, with a frame of reserve:

`v_limit = max(terminal, sqrt((a*dt)² + terminal² + 2*a*d) - a*dt)`.

The terminal value is 0 for the destination or `min(cornerSpeed, v_style)` for a bend; bend distance subtracts the bend acceptance radius. Target speed is the minimum of the style/arrival/braking envelopes. `Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration*dt)` preserves scalar acceleration/deceleration limits. Rotation still uses AgentRotation's limited `Quaternion.RotateTowards`; non-strafe speed is additionally scaled by `max(0, cos(remaining_yaw_error))`, with the existing turn-in-place gate. Strafe with a look target retains independent facing and is not alignment-throttled.

Clamp actual displacement to the current waypoint distance for long frames or a very close replacement path. This is a safety cap, not a look-ahead or teleport. Explicit Stop/Hold/invalid-route cancellation and final tolerance completion zero residual speed immediately; they are deliberate safety exceptions to acceleration-limited locomotion. A valid replacement retains scalar speed and clears old arrival state without firing Arrived for the superseded route. Arrived listeners can synchronously submit another command; the body then steers that new command, not an obsolete direction.

Stuck detection retains its timeout/event mechanism but reduces the progress threshold during slow motion to `min(minimumProgressDistance, max(0.0001, currentSpeed * stuckTimeout * 0.25))`. An intentional zero-speed turn resets the timer. True blocked translation still raises Stuck, without converting slow arrival into false blockage. No changes to IAgentBody, MoveCommand, MoveStyle, motor collision, event signatures or AI ownership.

### Inspector defaults / migration

| Setting | Default | Migration |
|---|---:|---|
| slowingRadius (new) | 1.5 m | Field initializer provides default on existing serialized components |
| cornerAcceptanceRadius (new) | 0.08 m | Maximum; also bounded by existing acceptance and adjacent segment lengths |
| cornerSpeed (new) | 0.5 m/s | Maximum approach speed, not a new movement style |
| Walk / Run / Sneak / Strafe | 1.6 / 4.0 / 1.0 / 1.8 m/s | Existing fields/values retained; default ordering Run > Strafe > Walk > Sneak > Idle |
| acceleration | 8 m/s² | Retained |
| waypoint / final radius | 0.4 / 0.15 m | Retained (Hunter's serialized 0.12 intermediate tolerance also retained) |
| turn rate / in-place gate | 360 deg/s / 100 deg | Retained |
| stuck timeout / progress | 2.5 s / 0.1 m | Retained; progress becomes adaptive near slow arrival |

No prefab or scene reconfiguration/bake is required. Existing serialized settings are not rewritten. Correct CharacterController dimensions, collision layers and valid waypoint clearance remain prerequisites. This body deliberately has no NavMesh query dependency: it cannot certify an arbitrary caller-supplied route over unbaked terrain. Pathfinders own walkability, and solid collider response owns blocking. Open holes, missing colliders, carving delays and invalid endpoint placement require the appropriate team's fixes, not steering shortcuts.

The original sandbox serializes CharacterController `minMoveDistance = 0.001`. A focused real Play Mode diagnostic stalled at `(0.03, 0.08, 0.03)` while commanded at 1.6 m/s, with `dt = 0.000147208` s: the requested displacement was below that threshold. AgentMotor now sets `minMoveDistance = 0` in Awake, allowing precise/high-frame-rate movement without rewriting any prefab or scene. Skin width and collision response are unchanged. This is an intentional runtime override of the controller's displacement filter, not a NavMesh/collision bypass; the independent flying body does not use AgentMotor.

### Cross-team handoffs

| Owner | Specific required input/configuration | Blocks this increment? | Smallest next action |
|---|---|---|---|
| Mahima — World Builder / Flanker | Main bake/connected routes, ground spawn clearance, reachable cover and patrol/flank points; actual BT path output and IAgentBody wiring | No for sandbox steering; yes for Main/Flanker acceptance | Supply one connected Main test route and one Flanker MoveCommand trace; place CoverPoint forward away from protecting obstacle |
| Chethiya — Systems / Breacher | Standalone prefab lacks serialized ground body and scene targets; existing PlayerControllerSandbox provides overrides and Unity supplies required health on load. Legacy mover must remain pathfinder-only while shared body controls movement | No: existing scene startup and scoped movement are executable; complete gameplay sequence is not yet accepted | Consolidate scene-only body setup into a reusable prefab if needed, supply spawn-time targets, leave legacy test movement off and run MoveTo → open/push → replan |
| Chethiya + Mahima | Door/barricade collider/carving and LevelChanged timing versus actual navigation connectivity; player pushing and spawn collision layers | No for static sandbox; yes for dynamic world acceptance | Close one doorway during a route and confirm collision, one Stuck/replan path, and new connectivity after opening/settling |
| Ashen — Core / Sentinel/models | Ground robot +Z orientation, rig, in-place animation controller with Speed float; action/hit/death mapping; independent Sentinel flight acceptance | No | Supply one ground model/controller specification; confirm Sentinel still uses SentinelDroneBody and its own visual axis correction |
| Manula — Agent Controller / Hunter | Hunter centroid-route regression and future graph invalidation after world changes | No for static steering | Run both existing Hunter scenes/tests; later coordinate cached-graph invalidation with LevelChanged without editing it in this task |

### Verification plan and remaining limitations

Run the Body fixture first, then the entire Unity EditMode suite. The new runtime cases explicitly enter/exit Play Mode and instantiate post-domain-reload assertion coroutines. They cover original AgentBodySandbox ping-pong, Run corner traversal/final slowing, command replacement/Stop/Hold/invalid and zero-length paths/reentrant Arrived, normal slow arrival versus a solid blocked route, and the actual Breacher MoveTo action Begin/Tick/Cancel against its own A* on an existing saved bake. Breacher runtime is loaded reflectively because it lives in Assembly-CSharp; no teammate assembly reference or runtime dependency is added. That case intentionally disables automatic GOAP Start, supplies only a temporary approach Transform in memory, and does not pretend to validate full planning/doors/death setup.

Existing Hunter tests are unmodified and remain the tests of custom A* detours, sound investigation and event/replanning integration. Breacher pure GOAP/executor/path tests are included in the full suite. There is no existing Sentinel or Flanker EditMode fixture; source isolation is not flight/BT Play Mode validation.

Manual checks: open `Assets/_Project/Scenes/Sandbox/Manula/AgentBodySandbox.unity`, press Play and watch both directions, corner slowing and final slowing/pause. Inspect new fields on TestRobot; no assignments are needed. Then open HunterSandbox and HunterNoiseSandbox separately, press Play and use the existing noise controls to observe the wall detour. For a configured Breacher, confirm Run → arrival polling → next GOAP action, replace a moving route, close a solid door, then reopen and replan. Test Strafe with an assigned look target and a real Animator Speed controller when supplied. Final Main, player-contact/crowd behavior, animated-model blending, dynamic route changes and live Sentinel flight remain joint/manual checks, not inferred passes.

### Changed-file inventory

```text
Assets/_Project/Scripts/AgentBody/AgentBody.cs
Assets/_Project/Scripts/AgentBody/AgentMotor.cs
Assets/_Project/Scripts/AgentBody/Logic/PathFollower.cs
Assets/_Project/Scripts/AgentBody/Logic/SteeringMath.cs
Assets/_Project/Scripts/AgentBody/Logic/SteeringMath.cs.meta
Assets/_Project/Tests/EditMode/AgentBody/PathFollowerTests.cs
Assets/_Project/Tests/EditMode/AgentBody/AgentBodySteeringTests.cs
Assets/_Project/Tests/EditMode/AgentBody/AgentBodySteeringTests.cs.meta
docs/decision-logs/agent-controller.md
```

### Executed test results — Unity 6000.3.25f1, 2026-10-20

- Final complete EditMode run: **95/95 passed, 0 failed, 0 skipped**. This includes **21 AgentBody cases** (16 pure/EditMode cases and 5 runtime integrations entering Play Mode), all existing Hunter tests and the existing Breacher pure planning/execution/path tests. The suite grew from 81 to 95 through 9 new pure cases and 5 new runtime cases.
- AgentBodySandbox completed both directions with two Arrived events and no Stuck. Runtime tests also passed tight corners/final slowing, valid replacement without obsolete arrival, immediate Stop/Hold cancellation, invalid/zero-length routes, reentrant arrival, Strafe cruise speed and independent rate-limited facing, slow progress without false Stuck, and collision blockage with Stuck rather than arrival.
- The actual Breacher MoveTo action runner used Breacher's own baked-triangulation A*, issued Run to the shared body, returned Running until real arrival, then Succeeded; Cancel cleared movement/arrival state and the legacy mover remained idle. This is not a full GOAP/door/barricade gameplay pass.
- Existing Hunter navigation-only test reached `(6.09, 0.03, -2.92)`, detour maximum Z `5.695`, 14 waypoints and 18 graph nodes. Existing sound/autonomy test also reached that goal, detour maximum Z `5.70`, with one injected stuck recovery. No Hunter code/test/scene was changed.
- Initial focused Body run: **20/21**, with the original sandbox stall traced to the CharacterController movement filter. A targeted diagnostic reproduced that failure and supplied the exact speed/dt/filter measurements above. After the motor fix, the first full run was **94/95**: the only failure was a new Strafe test asserting facing as soon as cruise speed was reached. It now waits for the independently rate-limited rotation; no steering-code relaxation was made. The subsequent complete run is the 95/95 result above.
- Final XML: `/tmp/hunter-stage-a.weTEGo/agentbody-final.xml`; log: `/tmp/hunter-stage-a.weTEGo/agentbody-final.log`. Runs used an isolated project with matching AgentBody scripts/tests and saved scenes, avoiding writes to the original project's Library/settings. Original AgentBodySandbox and teammate/Hunter assets are unchanged. `git diff --check` passed.
- **Not executed as a full acceptance scenario:** Breacher door/barricade/attack/death sequence, live Sentinel flight, Flanker BT (absent), final Main navigation/spawns (absent), real animation clips/controller and crowd/player-contact behavior. No compatibility claim is inferred solely from compilation. Manual instructions and owner handoffs above identify the remaining work.

Normal steering creates no per-frame collections, physics queries or graph rebuilds. Remaining-distance calculation is linear in the path's waypoint count; path lists are reused/copied on command boundaries. Tight corners may reduce total route speed and expose insufficient bake/controller clearance. Cruise ordering is unchanged, but safety limits can equalize style speeds near a bend. Explicit Stop is now immediate rather than coasting on a cancelled route. These behavior changes are intentional and require final gameplay-feel review, not an animation or pathfinding rewrite.

### Actual scene-load / startup audit

In an isolated Unity 6000.3.25f1 process, opened Main without saving: root count 2, NavMeshSurface count 0. Opened existing `PlayerControllerSandbox` without saving: all six Breacher target references assigned; AgentBody, CharacterController **and BreacherHealth present in the loaded instance**. Entered actual Play Mode; after Start the Breacher brain was enabled, shared body moving and legacy BreacherPathMover not moving. This corrects the initial raw-YAML suspicion about health: RequireComponent/loading supplies it even though its GUID was not present in the inspected serialization. No teammate configuration was changed or saved. Log: `/tmp/hunter-stage-a.weTEGo/agentbody-scene-audit.log`. Startup is not evidence that the whole door/push/attack/death sequence completes.

---

## 2026-10-20 — Final robustness increment: interrupted movement

**Baseline:** Inspected the existing first-increment pending diff and its 95-test state before editing. Branch remains `feature/agentbody-steering`. Existing settings, slowing/braking formulas, corner tolerances, style speeds, rotation rate, sandbox assets and public Core contracts remain unchanged. The unrelated untracked `ProjectSettings/TimelineSettings.asset` is untouched.

**Stop versus deceleration:** Retain the already verified immediate Stop/Hold cancellation semantics. Hunter uses Stop before attacking/replanning and Breacher uses it for GOAP cancellation; adding coasting towards a cancelled destination could cross a corner, drift into attack range errors or reinterpret the shared contract. The physically reasonable acceleration-limited braking envelope remains active during valid locomotion, arrival slowdown and route replacement. Explicit cancellation is a safety halt, not a new soft-stop action. Stop clears the path/destination, HasArrived, style, look target, progress tracking and speed, is idempotent, and now also clears reported horizontal motor velocity **synchronously**. It neither moves the controller nor resets gravity. Disable uses that same cancellation path. Hold preserves only its explicitly supplied look target. Invalid/empty commands cancel, with no arrival event or residual locomotion.

**Replacement:** A valid Execute resets completion/blockage state and never completes the old destination. Carry `min(previous commanded speed, last achieved motor speed)` into the new route; a controller physically stationary against a wall must not launch the recovery route at its unrealized cruise speed. Existing MoveTowards/braking limits then govern the new route. A same-list PathFollower replacement is now safe: its public read-only waypoint view may refer to the backing list, so do not clear that list before reloading it. Consecutive-duplicate and invalid-route handling from the first increment is retained.

**Stuck episode:** Previously a continuously blocked command could notify after every timeout. Latch the notification before invoking subscribers. Only useful measured waypoint progress, advancement to a new waypoint, or a fresh Execute rearms it; intentional turning/timer resets alone do not rearm continuous blockage. Stop/disable remove the command and therefore prevent subsequent normal detection. Stuck remains a notification, not a body-level replan or automatic path cancellation: IsMoving continues to describe the active path unless a listener chooses Stop/Execute. An unhandled blocked command can resume if the obstruction disappears; real progress allows one notification for a later blockage. No per-frame logging, timers/coroutines for deferred events, event-dispatch allocations or new external dependencies are introduced.

**Reentrant callbacks:** All arrival/stuck bookkeeping is done before the synchronous event. Arrival observers now see zero horizontal velocity. Stop or Execute inside a handler owns the resulting state; no old-route writes follow the notification. Normal C# multicast semantics are retained: an event already being delivered is a fact for that route, not a queued operation that can be retroactively withdrawn from other listeners. Cancellation suppresses future detections/events, not callbacks already executing. Hunter's existing reflection-based fault-injection tests still target the private notification helper; normal runtime detection remains guarded by active movement.

**Compatibility inspection:** HunterBrain still uses its own A*, sound memory and Arrived/Stuck handling; no Hunter file is edited. BreacherAgent polls HasArrived/IsMoving, sends Run and cancels with Stop; BreacherPathMover's independent transform loop must remain idle while shared AgentBody owns movement. Existing door and barricade LevelChanged/physics dependencies are unchanged; no complete dynamic GOAP gameplay claim follows from a scoped runner test. Sentinel still explicitly obtains SentinelDroneBody, which implements IAgentBody independently and does not use AgentMotor. No Flanker/BT implementation has appeared under Assets in this checkout. Main remains camera/light only with no NavMeshSurface or persisted scene navigation reference. First-increment cross-team handoffs remain applicable; no teammate modification is required to implement this increment.

**New/extended checks:**

- Stop during acceleration/cruise and near final arrival, repeated Stop, zero immediate velocity and no stale Arrived/Stuck across multiple timeout periods.
- Route replacement during acceleration, final slowing and a slow in-place turn; only the new destination arrives.
- One real collision-driven Stuck across repeated timeouts, rearming after obstruction removal and useful progress, a second real blockage and new-route recovery from actual rest.
- Real Stuck callback issuing Execute, then real arrival callbacks issuing Execute/Stop, without overwriting new state or producing late events.
- Very slow corner speed (0.05 m/s), arrival slowing and a prolonged turn with a short stuck timeout, without false notifications.
- Actual HunterBrain sound-driven replacement of an active investigation route, then arrival at the new sound and Search, using only shared noise events and read-only reflection from Body-owned tests.
- Actual Breacher MoveTo Begin/Tick/Cancel while moving: cancellation must not masquerade as GOAP success or produce later locomotion.
- Pure same-read-only-view PathFollower replacement without erasing its input.

**Exact files changed by this final increment (the earlier nine-file foundation diff is preserved):**
```text
Assets/_Project/Scripts/AgentBody/AgentBody.cs
Assets/_Project/Scripts/AgentBody/AgentMotor.cs
Assets/_Project/Scripts/AgentBody/Logic/PathFollower.cs
Assets/_Project/Tests/EditMode/AgentBody/PathFollowerTests.cs
Assets/_Project/Tests/EditMode/AgentBody/AgentBodySteeringTests.cs
docs/decision-logs/agent-controller.md
```
No new assets, metadata, assemblies or scene modifications are needed.

**Executed verification — Unity 6000.3.25f1:**

- Focused `BlackoutProtocol.Body.Tests` run: **28/28 passed, 0 failed, 0 skipped**. Includes 17 pure/EditMode cases and 11 runtime cases that enter and exit real Play Mode. XML: `/tmp/hunter-stage-a.weTEGo/agentbody-robustness-focused.xml`; log with the same prefix and `.log` extension.
- Subsequent complete EditMode suite: **102/102 passed, 0 failed, 0 skipped**. All previous 95 cases remain passing; six runtime cases and one pure same-view replacement case were added. These totals overlap; 102 is the complete-suite count, not 130 distinct cases. XML: `/tmp/hunter-stage-a.weTEGo/agentbody-robustness-all.xml`; log: `/tmp/hunter-stage-a.weTEGo/agentbody-robustness-all.log`.
- Original AgentBody sandbox still completed both directions without Stuck. Actual collision-driven notification counts, low-speed/turn tests, cancellation/velocity snapshots and reentrant callbacks all passed, rather than being inferred from compilation.
- Hunter: new real sound-driven active-route replacement test passed, including arrival at the replacement sound and Search. Existing Hunter tests passed unchanged, including the wall-detour investigation, injected stuck recovery, lifecycle, unreachable evidence and memory/search behavior. The existing detour tests again reached `(6.09, 0.03, -2.92)` with maximum Z about `5.70` beyond the obstacle.
- Breacher: extended actual MoveTo runner/A* integration passed Begin → Running → arrival → Succeeded, then fresh Begin → Cancel → Failed/non-moving/non-arrived with no later drift. Existing pure GOAP/executor/path tests also passed. No full door/push/attack/death gameplay completion is claimed; the prior scene startup audit remains historical evidence, not a newly executed full scenario.
- Sentinel: independent flying body/brain inspected and unchanged; no live flight test executed. Flanker remains absent, so no BT integration test was runnable. Main/navigation and dynamic-world handoffs remain those documented above.
- Sources/tests in the isolated verification project match the repository's current AgentBody directories. Tests do not write the original project Library/settings or save teammate scenes. `git diff --check` passes. Current branch remains `feature/agentbody-steering`; earlier uncommitted foundation files and unrelated TimelineSettings are preserved. No Git staging, commits, pushes, merges or branch switches were performed.

**Limits:** A Stop does not prevent an enabled brain from issuing a *new* command on its next tick, and cannot retract an event already being delivered. There are no queued movement-completion callbacks in this body. Pathfinders still own route validity; an unreachable physical route with a solid collider produces Stuck, not a fabricated shortcut or body-level replan. Static/dynamic NavMesh configuration, collider clearance, full GOAP obstacle gameplay and subjective visual feel remain the appropriate team/manual checks. No new visual sandbox session is claimed for this increment; automated runtime movement was executed and the first increment's user-verified appearance/settings are retained.

**Manual verification:** Open AgentBodySandbox and press Play. While accelerating or just before final arrival, use WaypointRouteDriver's **Stop** context menu; expect an immediate safety halt, no arrival/ping-pong restart from that cancelled command, and no continued drift. Use **Send Route** to resume. Repeat during a turn and observe the existing limited yaw and arrival slowing. For real blockage, place a solid cube across the route in Play Mode; use a debugger breakpoint on RaiseStuck or the focused test to count a single continuous-episode notification. Move it away and issue a new route; expect recovery, not an instant cruise-speed launch from rest. In HunterNoiseSandbox, issue a distant alarm then a stronger nearby sound while moving; verify retargeting and subsequent Search. Full Breacher door/push/attack/death gameplay, Main integration, live Sentinel flight and absent Flanker remain separate team/manual acceptance checks.
