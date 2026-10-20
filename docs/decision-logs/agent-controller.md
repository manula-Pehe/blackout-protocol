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

---

## 2026-10-20 — Animation increment 1: asset audit and reusable locomotion parameters

### Branch, evidence and supported scope

Verified `feature/agentbody-animation` before editing. Only pre-existing worktree item was untracked `ProjectSettings/TimelineSettings.asset`; it is untouched. No movement/steering script, Core contract, brain, teammate prefab, imported rig/model, scene, Package or ProjectSettings file is changed in this increment.

The **missing-assets fallback** is required. A Unity AssetDatabase/ModelImporter/subasset audit, not just filename searching, found:

- `Assets/_Project/Models/Export/PlasmaRifle.fbx`: Generic importer, **0 SkinnedMeshRenderers, no Avatar subassets, no embedded AnimationClips**.
- `Assets/_Project/Models/Export/SentinelDrone.fbx`: Generic importer, **0 SkinnedMeshRenderers, no Avatar subassets, no embedded AnimationClips**.
- Project-wide `t:AnimationClip`, `t:RuntimeAnimatorController` and `t:Avatar` searches under Assets returned no assets. Filename searches also found no Mixamo imports or other ground-character models. `.blend` source files are inventoried, not opened or edited; the imported exports above are what Unity can currently use.
- Breacher and Sentinel prefabs each have **0 Animators and 0 SkinnedMeshRenderers**. The standalone Breacher prefab has no shared AgentBody; its PlayerControllerSandbox instance supplies body overrides as established by the prior audit.
- Main, AgentBodySandbox, HunterSandbox, HunterNoiseSandbox and PlayerControllerSandbox each load with **0 Animators and 0 AgentAnimatorDrivers**. No existing visual animation connection can be presumed.

Exact Unity audit log: `/tmp/hunter-stage-a.weTEGo/agentbody-animation-audit.log`. The read-only audit used an isolated project, did not save these scenes, and left its temporary editor helper outside the repository.

### Cross-team asset/dependency matrix

`I/W/R/S/A/H/D` means Idle, Walk, Run, Strafe, Attack, Hit and Death clips respectively. **None of these seven clips is currently available for any ground agent.** No generic/weapon/drone import is substituted for a humanoid character.

| Agent | Available model/prefab and rig | Animator/controller; I/W/R/S/A/H/D | Movement/orientation now | Demonstration possible now; owner dependency |
|---|---|---|---|---|
| Hunter | No reusable Hunter prefab or rigged ground model found. Saved Hunter scenes use primitive torso/head/treads, not a skeleton or Avatar. | No Animator/controller; all seven clip categories absent. | HunterBrain sends Walk for wandering/investigation, Run for chase, Sneak for search to AgentBody/CharacterController. AgentRotation owns limited +Z-forward yaw. Body provides measured horizontal Velocity and ActionRequested; neither implies clips exist. | Actual speed parameters can be tested on the real brain using a transient test controller; **natural animation cannot be demonstrated**. Ashen supplies ground model/rig/clips; Manula wires the future child Animator/controller in Hunter-owned assets. |
| Breacher | `Prefabs/AI/Breacher/Breacher.prefab` is primitive/capsule-based, not a skinned ground robot or humanoid rig. Scene overrides add shared body components. | No Animator/controller; all seven categories absent. | BreacherAgent sends Run MoveCommands via custom A*, polls arrival and cancels with Stop; legacy BreacherPathMover direct-transform loop must stay idle. Shared AgentRotation faces travel; Run's optional look target is not a Strafe animation request. | Actual Begin/Cancel can drive a test Animator parameter; **full rigged locomotion/combat cannot**. Ashen supplies rig/clips, Chethiya owns prefab placement and combat/door timing, Manula supplies the generic driver. |
| Flanker | No Flanker prefab/model/Behaviour Tree implementation found in this checkout. Core CoverPoint alone is not an integration. | No Animator/controller; all seven categories absent/unavailable. | Movement writer, styles, facing and action lifecycle cannot be confirmed. | No real-agent demonstration. Mahima supplies BT/body integration and spawn/prefab configuration; Ashen supplies the compatible ground visual assets. |
| Sentinel | SentinelDrone.blend → SentinelDrone.fbx → SentinelDrone.prefab exists. Imported Generic **unskinned**, no Avatar; not a humanoid ground rig. | No Animator/controller or clips in any of the seven named categories. Ground Walk/Run states are not prerequisites for its procedural flight. | SentinelDroneBrain explicitly uses SentinelDroneBody : IAgentBody, with independent hover/collision/yaw/visual tilt and (-90,0,0) model-axis correction. It does not use AgentMotor/AgentAnimatorDriver. | Existing procedural drone presentation remains separate and untouched. Ashen owns any future flight/rotor animation; **do not add the ground driver**, whose RequireComponent would add AgentBody. No new flight validation is claimed. |

Main still contains camera/light only and no final agent spawns/NavMeshSurface. Mahima's Main, Flanker, cover and environment wiring remains a final-level dependency. There is no reason to modify Main to test this fallback.

### First supported implementation

AgentAnimatorDriver remains a ground-body component separate from every AI brain. It samples `AgentBody.Velocity` in **LateUpdate**, after movement's Update, rather than guessing locomotion from MoveStyle, requested speed, transform teleport deltas or IsMoving. Height/gravity is ignored. This matters when a Run command is physically blocked: visual locomotion must follow achieved speed, not pretend the body is running at cruise speed.

Inspector settings retain the existing animator reference and `speedDampTime` field:

| Setting | Default | Contract |
|---|---:|---|
| speedParameter | `Speed` | Required float parameter; name is configurable and hashed on binding changes |
| referenceSpeed | 1 m/s | `target = actualHorizontalSpeed / referenceSpeed`. Default preserves old raw-m/s units; reference equal to authored Run speed gives normalized Run = 1. Values above 1 remain visible, not silently clamped. |
| idleSpeedThreshold | 0.02 m/s | Smaller actual speeds map to exact Idle target 0, without changing physics |
| speedDampTime | 0.1 s | Exponential response time; 0 disables damping |

The driver owns a single damping stage:

`parameter += (target - parameter) * (1 - exp(-deltaTime / speedDampTime))`.

Values within 0.0001 of target snap to it so a stopped body reaches exact parameter zero. Invalid measured/config/time values are handled without NaN/Infinity output. Zero delta time preserves history. Walk/Run acceleration and braking change target continuously; Stop immediately makes physical speed zero and the **animation parameter fades towards Idle**. Execute/route replacement does not reset parameter history. There are no command/event subscriptions or combat-trigger mappings. Disabling the driver explicitly clears its parameter/state; it does not control or stop movement.

Root motion is forced off on binding and checked while active. The driver never writes a Transform, calls CharacterController.Move, chooses an action, or enables root motion. Assets must still have appropriate in-place root curves and must be validated visually once supplied; a parameter-only test does not prove a future imported clip cannot animate an unintended visual transform.

Binding diagnostics expose actual speed, target/smoothed parameter, readiness, missing Animator/controller/float parameter, inactive/uninitialized Animator and validation count. Missing/incompatible bindings produce **no recurring SetFloat warnings/log spam**. Animator parameter arrays are inspected only when the Animator, controller or parameter name changes (or on an explicit refresh), not every frame. Auto-discovery searches children, including inactive models, on enable/refresh. Use **Refresh Animator Binding** after attaching a model later or editing a controller's parameter schema in place; an explicit Animator reference takes priority. There are no new runtime assembly references, AI dependencies or per-frame managed collections.

### Combat/health audit — deferred, not silently implemented

Core AgentAction names Attack, Hit, Die and Alert and AgentBody.PlayAction dispatches ActionRequested. **AgentAnimatorDriver does not subscribe or play action states**; the older body's event comment is not evidence of a completed one-shot mapping. Hunter and Breacher request Attack but no matching clip/controller exists. BreacherHealth implements IDamageable and StateChanged; BreacherAgent stops/disables its AI when dead. PlayerHealth has IDamageable/Died, and PlayerWeapon applies damage through IDamageable. None of these automatically provides an enemy Hit/Death animation or correctly timed damage window.

Chethiya must agree on attack impact/cooldown and door-open/push timing before future animation events can affect gameplay. Ashen must provide the action clips and exit/interrupt conventions. Do not gate existing door movement, GOAP completion or damage on missing animations, and do not invent a new shared health/action interface in this increment.

### Exact inspection inventory

Source/API inspection (not modified unless listed below):
```text
Assets/_Project/Scripts/AgentBody/AgentBody.cs
Assets/_Project/Scripts/AgentBody/AgentMotor.cs
Assets/_Project/Scripts/AgentBody/AgentRotation.cs
Assets/_Project/Scripts/AgentBody/AgentAnimatorDriver.cs
Assets/_Project/Scripts/AgentBody/BlackoutProtocol.Body.asmdef
Assets/_Project/Scripts/Core/Movement/IAgentBody.cs
Assets/_Project/Scripts/Core/Movement/MoveCommand.cs
Assets/_Project/Scripts/Core/Movement/MoveStyle.cs
Assets/_Project/Scripts/Core/Movement/AgentAction.cs
Assets/_Project/Scripts/Core/Combat/IDamageable.cs
Assets/_Project/Scripts/AI/Hunter/HunterBrain.cs
Assets/_Project/Scripts/AI/Breacher/BreacherAgent.cs
Assets/_Project/Scripts/AI/Breacher/BreacherPathMover.cs
Assets/_Project/Scripts/AI/Breacher/BreacherHealth.cs
Assets/_Project/Scripts/AI/Breacher/BreacherBarricade.cs
Assets/_Project/Scripts/AI/Sentinel/SentinelDroneBrain.cs
Assets/_Project/Scripts/AI/Sentinel/SentinelDroneBody.cs
Assets/_Project/Scripts/Player/PlayerWeapon.cs
Assets/_Project/Scripts/Player/PlayerHealth.cs
Assets/_Project/Scripts/Interaction/SlidingDoor.cs
Assets/_Project/Tests/EditMode/AgentBody/BlackoutProtocol.Body.Tests.EditMode.asmdef
docs/decision-logs/core-developer.md
docs/evidence/core-developer/sentinel-drone-evidence.md
```
Asset/import/subasset or scene inspection:
```text
Assets/_Project/Models/Export/SentinelDrone.fbx
Assets/_Project/Models/Export/SentinelDrone.fbx.meta
Assets/_Project/Models/Export/PlasmaRifle.fbx
Assets/_Project/Models/Export/PlasmaRifle.fbx.meta
Assets/_Project/Prefabs/AI/Sentinel/SentinelDrone.prefab
Assets/_Project/Prefabs/AI/Breacher/Breacher.prefab
Assets/_Project/Scenes/Main.unity
Assets/_Project/Scenes/Sandbox/Manula/AgentBodySandbox.unity
Assets/_Project/Scenes/Sandbox/Manula/HunterSandbox.unity
Assets/_Project/Scenes/Sandbox/Manula/HunterNoiseSandbox.unity
Assets/_Project/Scenes/Sandbox/Chethiya/PlayerControllerSandbox.unity
```
Inventoried but not opened/edited as modelling sources: `Assets/_Project/Models/Source/SentinelDrone.blend` and `Assets/_Project/Models/Source/PlasmaRifle.blend`. Assets-wide searches checked animation controllers/clips/Avatars, Mixamo and Flanker; there is no missing filename to list for an absent asset.

### Exact changed files

```text
Assets/_Project/Scripts/AgentBody/AgentAnimatorDriver.cs
Assets/_Project/Scripts/AgentBody/Logic/LocomotionAnimation.cs
Assets/_Project/Scripts/AgentBody/Logic/LocomotionAnimation.cs.meta
Assets/_Project/Tests/EditMode/AgentBody/AgentAnimatorDriverTests.cs
Assets/_Project/Tests/EditMode/AgentBody/AgentAnimatorDriverTests.cs.meta
docs/decision-logs/agent-controller.md
```
There is deliberately **no saved animation sandbox/controller/clip** that could be mistaken for working humanoid animation. Tests create and destroy an in-memory parameter-only AnimatorController and empty motion. The existing movement sandboxes remain unchanged.

### Smallest handoffs and acceptance gap

| Owner | Smallest concrete delivery | Blocks current parameter increment? | Blocks visual animation acceptance? |
|---|---|---|---|
| Ashen | One ground robot model with a valid Humanoid Avatar/bone map (if retargeting Humanoid/Mixamo motions), or a matching authored Generic skeleton + clips; +Z visual forward/+Y up, confirmed scale/pivot; looping **in-place Idle/Walk/Run** clips with nominal locomotion speeds and root curves documented | No | **Yes**: cannot validate retargeting, stride/feet, poses or transitions without it |
| Manula / GV | Once those clips arrive, create an AgentBody-owned 1D locomotion controller/Blend Tree with a float Speed (or agreed name), configure thresholds in the chosen units, attach the model child/driver to a dedicated sandbox and tune reference/damping/clip timing against measured movement | Driver/math are implemented now; controller awaits actual clips | **Yes**, followed by actual graphical Play Mode evidence |
| Chethiya | Approve Breacher visual-child placement and reusable body prefab configuration; verify Run movement/Stop with that model. Later provide Attack/Hit/Death and door/push action timing expectations; keep legacy mover inactive when shared body owns motion | No | Breacher visual integration and later combat acceptance require this coordination |
| Mahima | Provide actual Flanker BT/prefab/body wiring and final Main agent spawns, valid navigation/cover placement and visual clearances | No | **Yes** for Flanker/final Main acceptance, not for a future isolated ground-animation sandbox |

Do not import arbitrary third-party characters, change Ashen's rig/importer or reuse the flying Sentinel as the missing ground skeleton without explicit approval. Strafe, Attack, Hit, Death, animation-event damage timing and complete blending polish are later increments, not part of this fallback.

### Manual verification

Today: run **Window > General > Test Runner > EditMode > AgentAnimatorDriverTests** (or Run All). Four runtime cases enter/exit real Play Mode; they inspect actual Animator float values but have **no humanoid pose to display**. Adding the driver alone to a primitive safely reports a missing binding; it does not make that primitive walk or supply missing clips. Read diagnostic properties through the debugger or a future debug view; no on-screen animation panel was added.

After the handoff: open AgentBodySandbox in Play Mode, attach the approved model as a child with an Animator/controller, and add AgentAnimatorDriver to the **AgentBody root**, not the drone. Assign Animator, choose parameter name and units, and leave root motion off. For example, with measured/authored Walk 1.6 m/s and Run 4 m/s and reference 4, future blend thresholds are Idle 0, Walk 0.4, Run 1; these are configuration examples, not constants in the driver. Observe acceleration, braking, corners, Stop fade-to-idle and route replacement in the Animator Parameters view and Game view. Confirm CharacterController alone moves the agent and the model faces +Z. Then repeat on approved Hunter/Breacher-owned instances without changing brains. A controller schema changed in place needs Refresh Animator Binding. Do not save teammate prefabs/scenes without their owner's approval.

### Actually executed verification — Unity 6000.3.25f1

- Unity's imported-asset audit completed successfully and produced the zero-controller/zero-clip/zero-Avatar results above. This is an actual Unity asset/subasset inspection, not a claim based on design documents.
- Initial focused AgentAnimatorDriverTests: **8/8 passed** (4 pure parameter-math cases and 4 runtime integrations entering/exiting actual Play Mode). Log/XML: `/tmp/hunter-stage-a.weTEGo/agentbody-animation-focused.log` and `.xml`.
- After adding the disabled-driver readiness guard and an explicit Animator-reference swap check, the final complete EditMode suite passed **110/110, 0 failed, 0 skipped**. It includes all 102 existing cases and all 8 new cases in their final form. Totals overlap; they are not 118 distinct tests. Final XML: `/tmp/hunter-stage-a.weTEGo/agentbody-animation-all.xml`; log: `/tmp/hunter-stage-a.weTEGo/agentbody-animation-all.log`.
- Runtime tests used a real Animator and transient float controller/empty motion to verify zero-speed Idle parameter, actual Walk/Run values with reference 4, braking response, Stop fade-to-idle, preserved history on route replacement, root-motion-off setting, missing/wrong/disabled/replaced bindings and configuration-only parameter validation. Test objects/controllers/motions were destroyed; no test assets were saved.
- Actual HunterBrain responded to shared sound events, requested Run through the unchanged AgentBody and drove measured animation parameters. The scoped actual Breacher MoveTo runner/A* drove nonzero parameters after Begin and faded to zero after Cancel, without claiming complete GOAP gameplay. Existing navigation, sound memory, stuck/arrival/search, steering and Breacher pure/runtime regression cases all remain green. Existing Hunter wall-detour integrations reached the goal at approximately `(6.09, 0.03, -2.92)`.
- **Graphical humanoid Play Mode evidence: none.** These were parameter/engine integrations in a headless Editor process, not a skinned-character visual session. There is no validated Avatar retargeting, real Idle/Walk/Run blend tree/state playback, stride matching, foot planting or natural ground-robot animation yet. Those checks are blocked by the identified assets and must not be inferred from 110 passing tests.
- Sentinel flight, absent Flanker integration, final Main spawning/navigation, full Breacher door/barricade/attack/death gameplay and future combat animations were not run as full acceptance scenarios. Inspection and the prior historical startup evidence are distinguished from new animation tests.
- The isolated project's AgentBody source/test directories match the final repository files. `git diff --check` passes; working tree changes are confined to the six files listed above, plus the pre-existing unrelated TimelineSettings entry. No new assembly/package/scene modifications, rig changes, Git staging, commits, pushes, merges or branch switches were performed. Stop after this supported first animation increment.

## 2026-10-20 — Animation Stage B: Titan robot locomotion integration

### Scope and actual input assets

Verified branch `feature/agentbody-animation`. Preserved all pending Stage A
driver/helper/test/log changes and the unrelated untracked
`ProjectSettings/TimelineSettings.asset`. Stage B does not edit those animation
implementation files, ground movement, shared contracts, AI brains, teammate
prefabs, Main, packages or settings. The original AgentBodySandbox is unchanged.

All four required FBX exports were present in
`/home/manula/Downloads/BlackoutRobots/`:

| Filename | Bytes | SHA-256 (original and repository copy match) |
|---|---:|---|
| Titan_Rigged.fbx | 2337728 | `5a4dab76f4b14ae55fc2c4f6c85e7b60bb7b185c10237059b6e63322d757d3a0` |
| Titan_Idle.fbx | 2886432 | `5d2d68a8495a3d5387e3d40260ee47dea36c6b85e8f87f8688f5d5fda567dd88` |
| Titan_Walk.fbx | 245712 | `e1efeb85faeba39021aba34d12f232dedcb8a3c4adb0eeac56717d25a2bea832` |
| Titan_Run.fbx | 222608 | `b48b94c35043d0dc9bb2d6d721411628011e177ebd44b513c17366b418917e5a` |

Copied the actual binaries unchanged under
`Assets/_Project/ThirdParty/AgentBody/Titan/`, following CONTRIBUTING's downloaded
asset convention rather than mixing source FBXs into team-owned Models. Originals
were not moved or modified. Native Unity controller/material/prefab assets live
under `Assets/_Project/Animations/AgentBody/Titan/` (GV ownership).
All binary FBXs already match the existing Git LFS rules; no staging/LFS conversion
or Git configuration changes were performed.

### Inspection and import decisions

Re-read `CONTRIBUTING.md`, `CREDITS.md`, `.gitattributes`, AgentAnimatorDriver,
LocomotionAnimation, AgentBody, AgentMotor, AgentRotation, WaypointRouteDriver,
the Body/test asmdefs, existing animation/steering tests and AgentBodySandbox YAML.
Checked the existing imported model conventions, prior four-agent dependency audit
above, and the actual FBX hierarchy/material/Avatar/clip subassets in Unity.

- Titan_Rigged: Humanoid, Create From This Model, animation import disabled. The
  generated Avatar is valid and human. Its skeleton has 34 mapped human bones.
- All four imports: Scale Factor **1**, **Convert Units / useFileScale disabled**,
  no axis correction or extra local scaling on the reusable visual root. Positive
  Z is the body/character forward direction and Y is up. CharacterController uses
  height 1.85, radius 0.3, centre Y 0.925; actual skeleton head is around Y 1.6 in
  the import rest pose. Conservative renderer bounds are larger than the mesh.
- Important export-specific correction: the automatic Avatar T-pose retained
  centimetre bone positions although imported transforms were metre-scale. An
  initial valid Avatar had `Animator.humanScale = 107.292` and produced severe
  limb stretching in real rendered playback. Neither a valid flag nor blindly
  setting Scale Factor 100 was sufficient. Configured the model's Humanoid T-pose
  skeleton metadata from the actual imported local transforms, retaining its human
  mapping. The final Avatar's human scale is **1.044674**, and the rendered poses
  are intact. This is serialized in `Titan_Rigged.fbx.meta`; no source rig/geometry
  was rewritten. Preserve this custom Rig configuration when reimporting; do not
  reset it to automatic raw-unit skeleton data.
- Idle/Walk/Run: Humanoid, Copy From Other Avatar referencing Titan_Rigged's Avatar;
  custom named clips, Loop Time and Loop Pose on. Root rotation, Y and XZ baked
  into pose; original orientation/Y retained. Runtime root motion is disabled.
  Imported lengths are approximately Idle 11 s, Walk 1.0333 s, Run 0.6333 s. The
  controller references only named clips, not Unity's preview subassets or the
  unused duplicated mesh in the Idle export.
- Supplied material: texture-free `CharacterMaterialmat`, already imported as
  URP/Lit, neutral grey (roughly 0.865 RGB). Preserved in a separate owned
  TitanSurface material. No texture maps accompanied the four downloads; the
  publisher's textured/PBR appearance is not claimed. Sandbox floor/route materials
  are also under the owned animation folder, not shared Materials.

### Controller, prefab and sandbox behavior

`TitanLocomotion.controller`: one default Locomotion state, no redundant
transitions, one 1D blend tree, float **Speed**, manual thresholds **Idle 0 / Walk
0.4 / Run 1**. Each clip plays at rate 1. The existing driver samples actual
horizontal movement in LateUpdate; sandbox reference speed is **4 m/s**, idle
threshold **0.02 m/s**, damping **0.1 s**. No AI action mapping or animation-driven
translation was added.

`TitanVisual.prefab` is a visual-only nested model prefab with valid Avatar,
controller and root motion disabled. It contains no AgentBody or CharacterController.
An owner can later attach it under a Hunter/Breacher body without installing a
second movement implementation. Do not attach the ground driver to Sentinel.

Open `Assets/_Project/Scenes/Sandbox/Manula/AgentBodyAnimationSandbox.unity`.
Press Play: initially Idle for 2.5 seconds, then Walk along a visible, hand-placed
U-route `(0,0,0) → (0,0,20) → (10,0,20) → (10,0,0)`. Arrival pauses in Idle for
2.5 seconds, then Run back, repeating Walk/Run alternation. Route markers sit just
below floor level; their Y value is immaterial to horizontal steering. This scene
tests animation and the existing shared movement; it does not need or modify a
NavMesh and does not demonstrate AI pathfinding.

The following camera frames the whole character from a rear three-quarter view.
On-screen buttons: **Walk**, **Run**, **Stop / Idle**, **Automatic**. Walk/Run
switches during movement preserve remaining waypoint progress; after Stop/arrival
a new route begins at the nearer endpoint without teleporting. Stop cancels
automatic progression. Automatic restarts with an Idle pause then Walk. Label
shows phase, measured m/s, smoothed Speed and binding readiness. Missing waypoints
or Stuck stop the demonstration with an explanatory label rather than retrying
each frame. OnDisable unsubscribes Arrived/Stuck and cancels this sandbox's route.
Only this testing component follows the camera and issues MoveCommands; it does
not duplicate motor, rotation, steering or animation-parameter algorithms.

### Exact Stage B file changes

Modified:

```text
CREDITS.md
docs/decision-logs/agent-controller.md
```

Added (all asset and folder metadata listed explicitly):

```text
Assets/_Project/Animations/AgentBody.meta
Assets/_Project/Animations/AgentBody/Titan.meta
Assets/_Project/Animations/AgentBody/Titan/SandboxFloor.mat
Assets/_Project/Animations/AgentBody/Titan/SandboxFloor.mat.meta
Assets/_Project/Animations/AgentBody/Titan/SandboxRoute.mat
Assets/_Project/Animations/AgentBody/Titan/SandboxRoute.mat.meta
Assets/_Project/Animations/AgentBody/Titan/TitanLocomotion.controller
Assets/_Project/Animations/AgentBody/Titan/TitanLocomotion.controller.meta
Assets/_Project/Animations/AgentBody/Titan/TitanSurface.mat
Assets/_Project/Animations/AgentBody/Titan/TitanSurface.mat.meta
Assets/_Project/Animations/AgentBody/Titan/TitanVisual.prefab
Assets/_Project/Animations/AgentBody/Titan/TitanVisual.prefab.meta
Assets/_Project/Scenes/Sandbox/Manula/AgentBodyAnimationSandbox.unity
Assets/_Project/Scenes/Sandbox/Manula/AgentBodyAnimationSandbox.unity.meta
Assets/_Project/Scripts/AgentBody/Testing/TitanAnimationSandbox.cs
Assets/_Project/Scripts/AgentBody/Testing/TitanAnimationSandbox.cs.meta
Assets/_Project/Tests/EditMode/AgentBody/TitanAnimationAssetTests.cs
Assets/_Project/Tests/EditMode/AgentBody/TitanAnimationAssetTests.cs.meta
Assets/_Project/ThirdParty/AgentBody.meta
Assets/_Project/ThirdParty/AgentBody/Titan.meta
Assets/_Project/ThirdParty/AgentBody/Titan/ATTRIBUTION.md
Assets/_Project/ThirdParty/AgentBody/Titan/ATTRIBUTION.md.meta
Assets/_Project/ThirdParty/AgentBody/Titan/Titan_Idle.fbx
Assets/_Project/ThirdParty/AgentBody/Titan/Titan_Idle.fbx.meta
Assets/_Project/ThirdParty/AgentBody/Titan/Titan_Rigged.fbx
Assets/_Project/ThirdParty/AgentBody/Titan/Titan_Rigged.fbx.meta
Assets/_Project/ThirdParty/AgentBody/Titan/Titan_Run.fbx
Assets/_Project/ThirdParty/AgentBody/Titan/Titan_Run.fbx.meta
Assets/_Project/ThirdParty/AgentBody/Titan/Titan_Walk.fbx
Assets/_Project/ThirdParty/AgentBody/Titan/Titan_Walk.fbx.meta
```

The earlier six-file Stage A increment remains pending, unchanged except for this
append-only GV log. Temporary Unity import/build/capture helpers, screenshots,
logs and XML results live only in `/tmp/hunter-stage-a.weTEGo`; no editor generator
or screenshots were added to the repository.

### Attribution and handoffs

CONTRIBUTING explicitly requires CREDITS entries alongside third-party imports
and assigns Animations to GV; no exclusive CREDITS owner is named. Added two
attribution rows rather than changing unrelated team credits.
[Sotudios' pack page](https://sotudios.com/Assets/Humanoid-RobotPack/) identifies
the model pack as CC BY 4.0; model credit, source, license link and derivative
changes are retained in CREDITS and the local ATTRIBUTION file. The
[Adobe FAQ](https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html) is linked
separately for the rigging/animation service; it does not replace the model license.
The four-file download did not include the pack's original license/metadata file.
Obtain and retain that original document before release, and review raw-asset
distribution terms before sharing outside the team. No original notice was removed.

| Owner | Smallest remaining handoff | Blocking Stage B? |
|---|---|---|
| Ashen / project owner | Supply original pack license/metadata and approved Titan PBR maps if textured appearance is wanted; assess robot proportions/rig and later combat clips. No Blender/drone/weapon edits needed. | Not blocking texture-free locomotion demo; release attribution evidence still needs original license document. |
| Chethiya | Approve attachment of TitanVisual to Breacher's existing body, renderer orientation/collider clearance, and keep independent legacy movement writers inactive. Future attack/door timing is separate. | Not blocking this owned sandbox. No teammate prefab changed. |
| Mahima | Supply actual Flanker body/prefab and Main spawn/navigation integration when ready; validate chosen visual clearances. | Blocks Flanker/final-scene integration only. No code/assets appeared in this task. |
| Manula / GV | Repeat graphical checks on approved Hunter/Breacher instances; tune authored gait speed/stride if desired. | Generic movement integrations tested; actual teammate prefabs not visually retargeted here. |
| Ashen / Sentinel | Retain independent flying movement and procedural visual tilt. | Unaffected; do not attach this ground locomotion stack. |

### Verification and limitations

Executed Unity 6000.3.25f1 against the isolated cached project
`/tmp/hunter-stage-a.weTEGo` to avoid generating unrelated repository settings.
Native assets and metadata produced there are copied together to the repository.
The FBX checksums above match the untouched downloads.

- Real graphics-enabled Editor Play Mode rendered camera captures of Idle,
  two different Walk poses, two Run poses and stopped Idle. Inspected these
  images; the corrected robot is intact and visibly changes pose. Logs show
  measured Walk ~1.6 m/s, Speed ~0.4; Run ~4 m/s, Speed ~1; stopped actual/parameter
  speed zero, root motion false. Files: `titan-visual-final.log`, `titan-idle.png`,
  `titan-walk-a.png`, `titan-walk-b.png`, `titan-run-a.png`, `titan-run-b.png`,
  `titan-stopped.png` in the temporary project. These are rendered Editor frames,
  not a claim of a human-operated continuous gameplay session or polished stride.
- Initial focused tests: 6/7 passed. The failing runtime check used WaitForSeconds
  in an EditMode-hosted Play Mode coroutine, so it sampled after only a frame
  rather than the intended acceleration interval. Replaced it with frame yields
  against explicit game time, matching the existing integration-test convention.
  Final focused run: **7/7 passed**, zero failed/skipped, in
  `titan-animation-focused-final.xml` / `.log`. The earlier failed result is
  retained in `titan-animation-focused.xml`, not represented as a passing run.
- New tests cover actual imports/Avatar configuration, all three looped root-baked
  clips, controller float/thresholds, reusable visual prefab and root motion,
  actual Humanoid bone motion, scale/stretch regression, actual Walk/Run parameter
  values, mid-route speed replacement, Stop returning to Idle and stationary
  body/visual root. Existing driver tests cover missing/wrong/replaced Animator
  and controller bindings.
- Final complete EditMode suite: **117/117 passed, 0 failed, 0 skipped** in
  `titan-animation-all.xml` / `.log`. This includes all previous 110 cases and the
  seven new Titan cases; focused counts overlap the full suite and must not be
  added as distinct tests. Existing actual Hunter noise/navigation/body and scoped
  Breacher Begin/Tick/Cancel integrations remain green. This is not a graphical
  retargeting acceptance run on their teammate-owned prefabs.
- A separate Editor process force-reimported all four FBXs using their saved
  `.meta` files, rebound the visual prefab Animator and sampled Walk. It passed:
  human scale **1.044674**, foot at approximately `(-0.18, 0.25, 0.35)`, controller
  TitanLocomotion and root motion false (`titan-reimport.log`). No dependence on
  the original automatic Avatar's cached import remains. Imported asset, script
  and test directories match the repository copies after this check.
- Final `git diff --check` passes; both new C# files also pass explicit untracked
  whitespace checks. Original AgentBodySandbox, body/motor/rotation, shared/team
  assets and unrelated TimelineSettings are unchanged. No staging, commit, push,
  merge, branch switch or PR creation occurred.

Manual follow-up: open the new scene in the repository Editor, press Play, watch
the automatic complete outbound/return route including corners and arrival Idle,
then press Walk/Run during movement and Stop. Inspect the child Animator Speed
and body velocity diagnostics. Test Runner > EditMode > TitanAnimationAssetTests
or Run All repeats the automated checks. Reopen Unity to confirm all GUID-backed
Avatar/controller/material/prefab references persist; no download-directory path
is used at runtime. Do not reset the FBX's custom Avatar T-pose configuration.

No Hunter/Breacher visual-prefab replacement, Flanker integration, Main setup,
Sentinel flight scenario, PBR texture authoring, foot IK/stride warping, Attack,
Hit or Death animation work was done. Walking/running clip cadence is not proven
perfectly stride-matched at all intermediate blend speeds; minor foot sliding or
clip seam cleanup remains an artistic acceptance check. The demo's hand-placed
route is not evidence of Hunter A* or dynamic obstacle avoidance. OnGUI text and
button allocations are sandbox diagnostics only; normal gameplay drivers retain
their existing cached parameter-binding behavior. Stop after Stage B.

## 2026-10-20 — Titan visual correction: original PBR maps and Run cadence

### Actual source audit

Branch remains `feature/agentbody-animation`; all prior pending animation work is
preserved. Found the extracted original pack at
`/home/manula/Downloads/humanoid-robot-pack-v1.0.0/`, including:

```text
characters/titan/textures/titan_basecolor.png
characters/titan/textures/titan_normal.png
characters/titan/textures/titan_roughness.png
characters/titan/textures/titan_metallic.png
metadata/titan.json
LICENSE.md
MIXAMO_WORKFLOW.md
```

The pack metadata specifies one material, 2048x2048 maps, 1.85 m height, 63,965
triangles and an unrigged source model. Its workflow states that the Mixamo-ready
upload is texture-free and retains the same UV layout as the textured source.
This explains why rigging/export did not automatically supply the textures.

The project owner had already copied those four PNGs, without `.meta` files, into
`Assets/_Project/Animations/AgentBody/Titan/Textures/` before this task. Reused
them unchanged and supplied their import metadata. SHA-256 hashes match the
extracted pack byte-for-byte:

| Map | SHA-256 |
|---|---|
| basecolor | `4d09a3c62cd06ad63ace506243615ffab90da1a155af4c5bcb480ea4d3d11653` |
| normal | `172c7b183b51cb325e789568e5041f8fed4f49ab1c44bd16d8d1262bdbefb60d` |
| roughness | `76ebc3e09248ca5c3ac95ac1d5eb7f54c5b27775df9e41fb23d276566b7e87ab` |
| metallic | `3afc91d8baf781f459ce101f4fcbe4e69182360e63e95f0c70e49ddf0b4e82cc` |

An initial duplicate import under ThirdParty was consolidated into those existing
paths before final verification, retaining the texture GUIDs. The unnecessary
copies created during this task are recoverably archived at
`/tmp/titan-duplicate-import.3cIyc5/Textures`, outside Assets; no pre-existing user
maps were deleted or overwritten. The final project has one active set of the
four source maps. Original package `LICENSE.md` and Titan metadata are now
retained alongside the model's attribution in ThirdParty; the previous missing
original-license handoff is resolved. No extra pack characters were imported.

Searched the actual FBX files throughout Downloads and shallow home locations.
The only downloaded animation candidate named Run/Running is
`/home/manula/Downloads/BlackoutRobots/Titan_Run.fbx` (222,608 bytes, unchanged
SHA-256 `b48b94c35043d0dc9bb2d6d721411628011e177ebd44b513c17366b418917e5a`).
Other pack character FBXs are unrigged models, not replacement animations. No
separate In Place Run export was found in those locations. Do not invent a
replacement or claim access to the original browser download settings.

### Material correction

Inspected the actual TitanSurface material and the SkinnedMeshRenderer nested in
TitanVisual: the renderer already referenced TitanSurface, but BaseMap, BumpMap
and MetallicGlossMap were empty. This was the direct cause of the white robot,
not a missing skin/Avatar or URP shader incompatibility.

Retained that same material and prefab reference, configuring URP/Lit:

| Input | Import / material use |
|---|---|
| Original basecolor | Default texture, **sRGB on**, Base Map; Base Color white to avoid tinting it |
| Original normal | **Normal Map**, non-color/sRGB off; Normal Map slot, normal scale 1, normal keyword on; no speculative green-channel flip |
| Original metallic / roughness | Default textures, **sRGB off**; preserved raw inputs to the derived map, not assigned incorrectly to smoothness |
| Derived TitanMetallicSmoothness | **R = metallic.R; A = 255 - roughness.R; G = B = 0**, linear/non-sRGB, input alpha preserved, Alpha Is Transparency off |

URP's installed `Shaders/LitInput.hlsl` reads metallic from the metallic map red
channel and smoothness from its alpha, multiplied by `_Smoothness`, unless the
albedo-alpha keyword is active. Set smoothness source to **Metallic Alpha**,
`_Smoothness=1`, enable `_METALLICSPECGLOSSMAP` and disable
`_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A`. Thus the final smoothness is exactly
`1 - roughness`; there is no incorrect gamma conversion or second inversion.
Legacy material tint is also white, matching URP Base Color.

Packing used raw PNG Color32 bytes in temporary linear Texture2D buffers, with
matching source dimensions and no resizing or RGB gamma conversion. The output
is a separate generated PNG; none of the four original images are edited.
Textures retain 2048 maximum size and mipmaps for this character demonstration.
CPU Read/Write is off in their gameplay importers; packing/testing buffers are
temporary, not retained per frame. Original materials, textures, lighting and
models belonging to teammates are untouched.

### Running diagnosis and smallest correction

Checked the actual controller, child scale/orientation, Avatar, source importer,
root-bake flags, loop samples and runtime speed diagnostics:

- Run is a 30 fps Mixamo take (`mixamo.com`), frames 0–19, about **0.633333 s**.
  There is no retained UI checkbox or named animation/overdrive setting in the
  provided files from which to reconstruct the original web download choices.
- An isolated Generic import of a copy of the original FBX, with uncompressed
  curves, shows hip travel of about **3.9937 m in 0.633333 s**: **6.305865 m/s**.
  Its source motion therefore is **not stationary/in-place**. The production
  Humanoid clip already bakes XZ/rotation/Y into pose, and the Animator already
  disables root motion, so no runtime root-motion conflict was found.
- Loop Time and Loop Pose are already enabled. Direct Humanoid playable sampling
  over the full cycle gives a maximum sampled seam position difference of about
  **0.00011 m**, with negligible angular discontinuity. No justified reason to
  trim frames, fabricate a new loop or add Animator state transitions was found.
- The Avatar remains valid/Humanoid with human scale **1.044674** and the previous
  metre-scale T-pose fix intact. Visual root scale is one and +Z remains forward.
  Bone scale/length checks and rendered armor/leg poses show no renewed stretching.
- AgentBody's actual Run remains **4 m/s**. The blend tree was playing the sprint
  clip at rate 1, despite its much faster authored travel/cadence. This gait-speed
  mismatch, rather than another rig overhaul, is the supported correction.

To avoid relying on raw root travel alone, sampled the retargeted toe trajectories
at 240 intervals/cycle. As a diagnostic approximation to stance, selected low toe
samples (within 0.06 m of each toe's minimum height), moving backward faster than
0.2 m/s with vertical speed under 1.5 m/s. For selected velocities `vZ`, fit the
playback multiplier `k` minimizing `sum((4 + k*vZ)^2)`:

`k = -4 * sum(vZ) / sum(vZ*vZ)`.

This yielded **0.6510** across 33 selected Run intervals. Approximate forward-slip
RMS drops from **2.2790 to 0.9138 m/s**; it is a useful diagnostic proxy, not a
claim of perfect foot planting. Source travel predicts `4 / 6.305865 ≈ 0.6343`,
which independently supports slowing the clip. Chose the simple fitted value
**0.65** for the Run blend-tree child's **Time Scale only**. The resulting full
Run cycle is roughly **0.9744 s**. Idle/Walk child rates remain 1, Speed thresholds
remain 0 / 0.4 / 1, reference speed remains 4 m/s and parameter damping remains
0.1 s. No body speed, acceleration, stopping, AI or movement code changes.

No replacement download was necessary for this scoped correction. If an artistic
review still rejects this high-knee sprint on a heavy robot, the smallest next
handoff is a genuinely downloaded, less exaggerated Mixamo running/jogging loop
exported **In Place, 30 fps, Without Skin** for the same Titan character. Its exact
clip identity must be supplied/inspected, not invented. Foot IK/stride warping and
combat animations remain outside this task.

### Exact changes in this visual-correction increment

Modified existing files (including files still untracked from the previous stage):

```text
Assets/_Project/Animations/AgentBody/Titan/TitanSurface.mat
Assets/_Project/Animations/AgentBody/Titan/TitanLocomotion.controller
Assets/_Project/Tests/EditMode/AgentBody/TitanAnimationAssetTests.cs
Assets/_Project/ThirdParty/AgentBody/Titan/ATTRIBUTION.md
docs/decision-logs/agent-controller.md
```

Added files:

```text
Assets/_Project/Animations/AgentBody/Titan/Textures.meta
Assets/_Project/Animations/AgentBody/Titan/Textures/titan_basecolor.png.meta
Assets/_Project/Animations/AgentBody/Titan/Textures/titan_normal.png.meta
Assets/_Project/Animations/AgentBody/Titan/Textures/titan_roughness.png.meta
Assets/_Project/Animations/AgentBody/Titan/Textures/titan_metallic.png.meta
Assets/_Project/Animations/AgentBody/Titan/TitanMetallicSmoothness.png
Assets/_Project/Animations/AgentBody/Titan/TitanMetallicSmoothness.png.meta
Assets/_Project/ThirdParty/AgentBody/Titan/LICENSE.md
Assets/_Project/ThirdParty/AgentBody/Titan/LICENSE.md.meta
Assets/_Project/ThirdParty/AgentBody/Titan/titan.json
Assets/_Project/ThirdParty/AgentBody/Titan/titan.json.meta
```

Existing user-provided untracked PNGs reused unchanged (not newly authored or
overwritten by this increment):

```text
Assets/_Project/Animations/AgentBody/Titan/Textures/titan_basecolor.png
Assets/_Project/Animations/AgentBody/Titan/Textures/titan_normal.png
Assets/_Project/Animations/AgentBody/Titan/Textures/titan_roughness.png
Assets/_Project/Animations/AgentBody/Titan/Textures/titan_metallic.png
```

No scene, visual-prefab, Animator/Avatar/FBX importer, FBX binary, runtime script,
Core contract, teammate asset, package or ProjectSettings file was changed in
this increment. No new production abstraction or per-frame processing was added.
The prior Stage A/B file manifest describes earlier pending changes, not this
increment's edits. CREDITS remains as previously recorded; existing Sotudios and
Mixamo credits cover these matching maps. TimelineSettings SHA-256 remains
`de43ff94910a26c658e7228213b553ac66f7c5d27cb513c3bc6ba2924eb0455c`.

### Actually executed verification

- Unity **6000.3.25f1**, isolated project `/tmp/hunter-stage-a.weTEGo`. The shader
  implementation was inspected in the installed URP package, not guessed from
  another rendering pipeline's channel layout.
- Graphics-enabled Editor **Play Mode**, normal time scale: exercised the same
  public methods as Walk, Run, Stop and Automatic buttons. Rendered and inspected
  original-color Idle, different Walk/Run poses, an intermediate Walk→Run blend,
  stopped Idle and both automatic arrival poses. The complete Automatic Walk
  outbound and Run return completed successfully; it was not just compilation or
  a stationary model preview. Runtime pose checks retain the expected human scale
  and root-motion-off condition. The camera captures show the original yellow
  and dark-metal armor with normal/material detail and no severe limb stretching.
- Evidence in the temporary project: `titan-corrected-visual.log` and
  `titan-corrected-{idle,walk-a,walk-b,walk-run-blend,run-a,run-b,run-c,run-loop,run-loop-b,stopped,automatic-walk,automatic-walk-arrived,automatic-run,automatic-run-b,automatic-run-arrived}.png`.
  Walk arrival was at game time ~53.123 s; Run return arrival at ~70.111 s.
  Stop held the body at its cancelled position. Intermediate blend captured
  actual speed ~3.24 m/s with Speed ~0.651; steady Run remained ~4 m/s / Speed 1.
  These are scripted graphics-enabled Play Mode camera captures, not a claim of
  human-operated UI testing or a recorded continuous-video artistic review.
- Loop/contact/source audit: `titan-run-contact-audit.log`; material setup:
  `titan-material-fix.log`; cadence edit: `titan-run-cadence-fix.log`. These helpers
  and the temporary Generic source copy were not added to the repository.
- Focused **10/10 passed, zero failed/skipped** in `titan-correction-focused.xml`
  / `.log`: seven existing Titan cases plus material/import assertions, exact
  every-pixel metallic/roughness packing and continuous, stable-scale Run pose
  sampling. Existing controller test now checks rates 1 / 1 / 0.65. Texture GUIDs
  were retained when consolidating source paths after that focused run; final
  complete-suite verification uses the final locations and matching tests.
- Final complete EditMode suite: **120/120 passed, zero failed and zero skipped**,
  in `titan-correction-all.xml` / `.log`. This includes all prior 117 cases plus
  the three new regression cases, with the final source-map paths and corrected
  material/controller. Existing Hunter/Breacher and AgentBody integrations remain
  green. Focused tests overlap the full suite and are not extra distinct cases.
- Final imported asset and test directories match the repository. All original
  FBX and texture hashes remain unchanged. `git diff --check` and the explicit
  untracked test-file whitespace check pass. The sandbox scene, visual prefab,
  runtime animation/movement scripts and previous Avatar metadata are unchanged
  by this correction. TimelineSettings hash still matches the pre-task value.
  No commits, staging, pushes, merges or branch switches were performed.

### Manual acceptance and remaining limits

Open `Assets/_Project/Scenes/Sandbox/Manula/AgentBodyAnimationSandbox.unity` and
press Play. Titan should now have yellow/dark armor rather than white. Watch the
automatic route and arrival Idle; use Walk→Run mid-route, then Stop and Automatic.
Check actual m/s and Speed labels, the child Animator's disabled Apply Root Motion,
and Run child's 0.65 Time Scale. The CharacterController remains the only movement
authority. Test Runner > EditMode > TitanAnimationAssetTests or Run All repeats
the automated checks. No external download path is needed at runtime.

The source sprint's high-knee style and some residual contact drift/armor overlap
remain possible, particularly at blended intermediate speeds and tight turns.
No new foot IK, warping, compression tuning, root motion, Animator layering or
robot-specific animation authoring is claimed. Source normals are used with the
standard non-color Normal Map import; no artist-directed normal reversal was
provided or invented. The original pack license is now retained, but team approval
of final artistic style and use on Hunter/Breacher-owned prefabs is still separate.
Do not turn a sandbox visual fix into Main or teammate prefab integration.

## 2026-10-20 — Hunter animation integration test: pre-LateUpdate race

### Evidence and root cause

Investigated the reported failure on `feature/agentbody-animation`, Unity
6000.3.25f1. Read the complete AgentAnimatorDriverTests fixture, transient
ParameterRig and wait helper; traced HunterBrain's event subscription, sound
memory/Utility selection, A* request and cancellation paths, HunterNavMeshPathfinder,
AgentBody/AgentMotor, AgentAnimatorDriver and locomotion math. Checked the real
HunterNoiseSandbox, baked navigation, wander points and manual noise trigger.

The exact zero asserted at the original line 181 is **TargetSpeedParameter**.
That assertion happens **before the test emits its gunshot**. It is not an
assertion on motor velocity or on Animator.GetFloat, and does not establish a
sound-perception failure. The driver's default reference is 1, so this fixture's
target speed uses actual m/s rather than normalized Run=1 units.

The old wait required only Animator binding readiness and body velocity >0.1.
Binding readiness does not mean AgentAnimatorDriver has sampled movement. A
newly attached driver has zero-valued speed fields until LateUpdate; the Hunter
may already have moved through AgentBody.Update/CharacterController. If the
Animator is already initialized, the wait can finish in that same frame, before
the driver's first LateUpdate. The target and stored Animator float are still
zero. This explains why cold/headless timing could pass while the real GUI Editor
failed; adding an arbitrary frame delay would not express the actual contract.

Preserved the user's latest real-Editor XML (a single-test failure at line 181)
as `/tmp/hunter-stage-a.weTEGo/hunter-animator-user-editor-original.xml` before
running isolated tests. The user also reported 119/120 in the full GUI suite.
The repository project had a live Editor/UnityLockfile; did not close it or start
a competing process against that project.

Actual reproduction using matching scene/scripts in a separate graphics-enabled
Editor process:

- Original test with diagnostic logging: **1/1 passed**, confirming intermittent
  scheduling rather than claiming to reproduce it on every run
  (`hunter-animator-before-graphics.xml` / `.log`).
- Controlled realistic precondition: wait for the autonomous Hunter to physically
  move, then attach the transient driver; leave the old readiness/velocity wait
  unchanged. **1/1 failed at the same positive-target assertion**
  (`hunter-animator-race-repro.xml` / `.log`). Both snapshots were frame **234**,
  delta time ~**0.000828 s**, body velocity approximately `(-0.03,0,-0.10)`,
  Walk, waypoint 1/5, Animator initialized/Ready. Driver actual, target, smoothed
  and stored Animator Speed were **all exactly zero**. No fake velocity, Animator
  float, physics movement, mocked brain or manually invoked driver update was
  injected. This isolates the pre-LateUpdate race without a production change.

### Smallest correction, within AgentBody ownership

Only these two files changed in this increment:

```text
Assets/_Project/Tests/EditMode/AgentBody/AgentAnimatorDriverTests.cs
docs/decision-logs/agent-controller.md
```

- Retain the physically-moving-Hunter precondition before attaching ParameterRig,
  so the regression covers attaching a visual driver to an already moving body.
- Replace the incomplete wait with a **bounded observable pipeline condition**:
  horizontal motor velocity, driver's sampled horizontal speed, target parameter
  and stored Animator Speed must all be positive; body must actually displace
  more than 0.02 m. The old positive-target assertions remain and explicit
  positive Animator assertions are added. No Animator values are written by the
  test to satisfy the assertions.
- After the real shared gunshot event, assert acceptance count increases; wait
  for actual Chase, a new path request, Run style, physical displacement and
  positive sampled/Animator outputs. Retain target/sample equality and
  stored/smoothed equality, plus root-motion-off checks. Observed A* routes have
  five waypoints and zero failures, so unreachable navigation was not the cause.
- Exercise HunterBrain's real **OnDisable → AgentBody.Stop** lifecycle path,
  without editing it. Assert velocity/sample/target/smoothed/stored values settle
  to exact zero within 3 seconds and horizontal position does not drift. This
  verifies locomotion Idle, not a nonexistent Hunter Utility action named Idle;
  the disabled brain may correctly retain its previous CurrentAction value.
- Existing 4-second movement deadlines remain. The test wait helper accepts an
  optional failure-only diagnostic snapshot: action/status, accepted noise and
  path counts/failures, waypoint index, grounding, body velocity, binding state,
  initialization and every speed stage. Three successful phase snapshots are
  logged by this test only. No per-frame production logging or allocation change.
- No edits to production driver/motor/steering, Hunter/Breacher code, shared Core,
  assemblies, scenes, animation assets, Packages or ProjectSettings. Reflection
  follows the existing Body test convention and adds no assembly dependency.

### Verification

Results and logs are under `/tmp/hunter-stage-a.weTEGo`:

| Actually executed run | Result | XML / log basename |
|---|---|---|
| Corrected Hunter test, independent graphics run 1 | **1/1 passed** | `hunter-animator-fixed-graphics-1` |
| Corrected Hunter test, independent graphics run 2 | **1/1 passed** | `hunter-animator-fixed-graphics-2` |
| Corrected Hunter test, independent headless run 3 | **1/1 passed** | `hunter-animator-fixed-headless-3` |
| Complete AgentAnimatorDriverTests fixture, graphics enabled | **8/8 passed** | `hunter-animator-driver-group` |
| Complete EditMode suite, headless | **120/120 passed, 0 failed, 0 skipped** | `hunter-animator-all` |

Repeated/group/full runs overlap, not extra distinct tests. These runs use Unity
6000.3.25f1 with matching Body/Hunter scripts, Body tests, scene and baked NavMesh;
they do not claim a rerun inside the user's already-open GUI Editor. Full-suite
Hunter output again shows successful five-waypoint Wander and Chase A* routes,
one accepted gunshot, two path requests, no path failures, positive matched
driver/Animator output, then cancellation with all speed stages exactly zero.
Breacher and all existing movement/animation regressions also remain green.

Final `git diff --check` and the explicit untracked test-file whitespace check
pass. The only edits in this investigation are the two files listed above. All
prior pending work is retained, with no new production or asset changes. The
unrelated TimelineSettings SHA-256 remains
`de43ff94910a26c658e7228213b553ac66f7c5d27cb513c3bc6ba2924eb0455c`.
No Git staging, commit, push, merge or branch switch occurred.

Manual GUI follow-up: after compilation, rerun
`AgentAnimatorDriverTests.HunterActualSoundResponse_UsesSameDriverWithoutBrainChanges`
several times, then its fixture and Run All in EditMode. Each case enters Play
Mode itself. Output should show Wander with positive measured/Animator speed,
accepted gunshot/Chase/Run with a new route and no failures, then cancelled/Idle
with all speed values zero. If a machine-specific timeout occurs, retain the new
full-state snapshot rather than weakening the positive assertions or increasing
delays blindly. No separate graphical character-pose or art change is involved:
this test intentionally uses a transient parameter-only Animator with empty motion.
