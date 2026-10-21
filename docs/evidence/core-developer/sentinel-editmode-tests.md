# Sentinel EditMode Tests

The Sentinel unit tests live in `Assets/_Project/Tests/EditMode/Sentinel/` and exercise the same decision and graph-search logic used by the runtime brain and NavMesh pathfinder. The test assembly is `BlackoutProtocol.Sentinel.Tests.EditMode`.

## Run the tests

In Unity, open **Window > General > Test Runner**, select **EditMode**, expand `BlackoutProtocol.Sentinel.Tests.EditMode`, then run `SentinelHsmTests`, `SentinelDecisionLogicTests`, and `SentinelGraphSearchTests`.

## Coverage

- HFSM transitions through Patrol, Alert, Track, Search and Return, including reacquiring the player during Search.
- The active Search child exits when the Contact parent changes to the Routine parent.
- Patrol selection skips unreachable points, prefers the least-recently-visited reachable option after its route-length penalty, and avoids repeating the current point when alternatives exist.
- Predictive search places the candidate ahead along the last known heading first, then ranks lateral checks and the last-known position.
- Search candidate selection skips unreachable predictions and finds the next reachable point. Alarm cooldown logic suppresses repeated alarms until the interval expires.
- The HFSM returns to Patrol when the player dies.
- Lethal drone damage stops brain movement and enables Rigidbody gravity for the fall.
- A* and UCS find equal-cost paths on the same graph; the test checks A* expands no more nodes on the selected graph. A disconnected goal fails, and start-equals-goal returns the single-node zero-cost path.

## Verification record

On 2026-10-21, Unity compiled scripts with zero warnings. The Sentinel EditMode run completed with **13 tests passed and 0 failed**. The full project EditMode suite timed out during an earlier run, so this record makes no claim about unrelated test assemblies. For edge-case coverage and runtime checks, see `sentinel-edge-cases.md`.
