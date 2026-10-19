# Systems Engineer Decision Log

## CharacterController movement

- Used Unity's `CharacterController` for responsive first-person movement, slope/step handling, and predictable collision behavior.
- Kept gravity as an explicit velocity so jumping and grounded behavior are explainable and tunable.
- Added a centre-of-mass force bridge for dynamic Rigidbodies because a `CharacterController` does not push them automatically. Applying force at the centre avoids unwanted crate torque.

## Noise and footsteps

- Footsteps are distance-based rather than timer-based, so step frequency follows actual player travel.
- Walking emits a 3 m AI-hearing event, running emits 8 m, and crouching emits none.
- A downward ray detects `FootstepSurface`; metal applies a 1.5 noise multiplier while concrete uses 1.0. Surface-specific clip arrays allow audio variation without changing AI logic.

## Pushable objects

- Heavy crates use dynamic Rigidbodies with interpolation, damping, frozen X/Z rotation, and Discrete collision detection because their speed is capped at 3 m/s.
- A moving crate temporarily disables NavMesh carving. After remaining stationary for 0.5 seconds it sleeps, restores carving, and raises `GameEvents.LevelChanged` once.
- Player pushing and AI pushing are kept separate: the player applies continuous centre force, while `IPushable.Push` applies an agent-requested impulse.

## Pickup, throw, and impact

- Held objects become kinematic and ignore collisions with the holder, preventing jitter against the Player collider.
- Released objects use Continuous Dynamic collision detection because throwing makes them the only fast physics objects in this feature set.
- Significant impacts raise a shared `NoiseType.Impact` event with speed-scaled loudness and a cooldown to prevent bounce spam.

## Interaction architecture

- Doors, switches, throwables, and the shutdown console implement the same ray-driven `IInteractable` contract.
- The shutdown console exposes both a C# event and an Inspector `UnityEvent`, allowing game flow to connect without adding a direct dependency.
- Audio clips are optional references; gameplay events remain functional when final sound assets are unavailable.

## Physics optimization

- Fixed timestep remains 0.02 seconds (50 physics updates per second), balancing stable interaction with CPU cost.
- Doors are kinematic and use `Rigidbody.MovePosition`; they do not require dynamic simulation.
- Interaction raycasts use an explicit layer mask and the non-allocating single-hit `Physics.Raycast` overload.
- Rigidbody interpolation is restricted to visible moving interaction objects.
- Sleeping and settle detection prevent stationary crates from remaining active physics bodies.
