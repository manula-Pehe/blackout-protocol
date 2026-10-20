# Player Combat Feedback

`PlayerCombatFeedback` connects to `PlayerWeapon` shot, hit, reload, and empty-shot events in `PlayerShootingTest`.

- Firing plays an optional assigned shot clip or the built-in synthesized fallback, emits a configurable camera-side muzzle flash, briefly lights the muzzle area, and adds a small upward camera kick.
- A raycast hit emits a configurable burst of sparks at the impact point, oriented to the hit surface.
- Reloading and empty-magazine attempts play optional assigned clips or their built-in synthesized fallbacks.
- Muzzle and impact particle systems are created once and reused. The sound clips are synthesized once at runtime and reused with `PlayOneShot`.
- The effect uses a child named `Muzzle` when available. In the shooting sandbox, which has no weapon mesh, it uses a camera-relative fallback anchor.

## Audio and visual options

Select the Player object in the sandbox and use the `PlayerCombatFeedback` component. Assign imported AudioClips to `Shot Sound`, `Reload Sound`, or `Empty Sound` to audition replacements; leave a slot empty to use its synthesized fallback. The component also exposes the muzzle and impact colors, particle counts, muzzle light intensity and range, and recoil amounts.

## Sandbox check

Open `Assets/_Project/Scenes/Sandbox/Ashen/PlayerShootingTest.unity` and press Play. Fire at the target and a wall to see the muzzle flash, camera kick, and impact sparks. Empty the magazine to hear the dry click, then press R to hear the reload sound. Check that the ammo, health, and reload HUD still updates.

The particle objects are pooled; capture a Unity Profiler CPU/GC Alloc trace during repeated firing before using it as optimization evidence.
