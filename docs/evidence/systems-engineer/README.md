# Systems Engineer Evidence

Capture the following Unity screenshots before submission and save them in this folder.

| File | Unity view | Evidence to show |
|---|---|---|
| `physics-collision-matrix.png` | Project Settings > Physics | Named gameplay layers and unnecessary collision pairs disabled |
| `fixed-timestep.png` | Project Settings > Time | Fixed Timestep = 0.02 seconds |
| `crate-rigidbody.png` | PushableCrate Inspector in Play Mode | Mass 35, interpolation enabled, Discrete detection, X/Z rotation frozen |
| `throwable-rigidbody.png` | ThrowableCanister Inspector after throwing | Mass 2, interpolation enabled, Continuous Dynamic detection |
| `navmesh-carving.png` | PushableCrate NavMeshObstacle Inspector | Carving and Carve Only Stationary enabled |
| `physics-profiler-moving.png` | Profiler > Physics while pushing/throwing | Active dynamic bodies during interaction |
| `physics-profiler-sleeping.png` | Profiler > Physics after objects settle | Reduced active bodies after the crate and canister sleep |
| `interaction-noise-console.png` | Game and Console | Surface footsteps, door noise, impact noise, and LevelChanged logs |

## Profiler procedure

1. Open Window > Analysis > Profiler and select the Physics module.
2. Enter Play Mode and clear the Profiler timeline.
3. Push the crate and throw the canister; capture a frame while they are moving.
4. Stop interacting and wait several seconds; capture a frame after the Rigidbodies sleep.
5. Record the active/dynamic body counts visible in both screenshots.

## Reasoning summary

- Slow heavy crates use Discrete collision detection; the fast thrown canister alone uses Continuous Dynamic.
- Kinematic doors avoid unnecessary dynamic simulation.
- Stationary pushables sleep and carve the NavMesh only after settling.
- A 0.02-second fixed timestep is sufficient for the capped crate speed and continuous throwable collision mode.
