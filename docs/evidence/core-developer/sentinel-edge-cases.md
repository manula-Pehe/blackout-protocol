# Sentinel Edge Cases

## Changes

- A lost target continues through the predictive search points. If a point cannot be reached, the Sentinel skips it and tries the next reachable candidate.
- A body stuck during Patrol, Track, Search, or Return causes the brain to select another destination or re-score its options.
- Tracking observations are refreshed so the Sentinel can replace an occluded or stale observation point.
- A `LevelChanged` event rebuilds the Sentinel pathfinder and replans its current destination.
- Alarm publication has a six-second cooldown. An alarm raised by another source also starts the cooldown, preventing nearby Sentinel instances from immediately duplicating it.
- If the player dies, the Sentinel returns to Patrol.
- Lethal damage stops and disables brain movement, then enables Rigidbody gravity so the drone falls under physics. Further damage is safe to receive.

## Verification

- Unity compiled the updated scripts with zero warnings.
- The targeted Sentinel EditMode run passed all 13 tests with no failures. New checks cover unreachable search candidates, the alarm cooldown, player death returning the state machine to Patrol, and lethal damage stopping the brain and enabling the fall.
- Existing sandbox scenario evidence covers patrol, spotting, alarm, tracking, loss of sight, search, return, and resuming patrol. See `sentinel-drone-evidence.md`.
- The stuck-destination recovery, moving-obstacle re-score, and level-change replan are implemented in the runtime brain and are not individually exercised by the added EditMode tests.

## Manual check

In the Sentinel sandbox, start Play Mode and confirm the drone patrols, raises one alarm when it spots the player, searches after losing sight, and resumes Patrol. To check recovery cases, block its current route or change the level geometry while it is moving. To check shot-down behavior, apply lethal damage and confirm it stops steering and falls. These runtime checks should be observed in the scene; the EditMode results above do not replace them.
