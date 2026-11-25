# Main scene live Hunter observations — Unity 6000.3.25f1

Captured with the official Unity MCP `script_eval` against the open Main scene in Play Mode. These are observations from this Editor session, not automated pass assertions. The player was placed at a nearby sampled NavMesh point and a **player-sourced** `GameEvents.RaiseNoise` gunshot was emitted. No Hunter action, Animator state, or impact callback was forced.

| Observation | Live value |
| --- | --- |
| Initial Hunter / player position | `(8.00, 1.08, 8.00)` / `(8.00, 1.08, 11.17)` |
| Acoustic acceptance | `AcceptedNoiseCount` increased to 1; perceived loudness `24.5 / 30.0`, estimated uncertainty `1.1 m` |
| Natural attack | `CurrentAction=Attack`, `AttackCount=1`, Hunter/player separation `0.72 m`; status said attack executed at a recently heard player within clear melee range |
| Animation-synchronized damage | Player normalized health was `0.900` after the first attack; following a second player-sourced gunshot, health fell to `0.500` while `AttackCount` reached 4. `HunterBrain` uses the timed attack callback and revalidates on impact. |
| Later action | `Search`, then `Wander` as sound evidence aged. |
| Main graph | 291 nodes during the route comparisons. Several default Wander goals were unreachable, producing throttled route failures; nearby attack pursuit was successful. |

The five successful A*/UCS samples in `search-comparisons-20261125.csv` used identical cached graph, start and goal anchors. Both algorithms returned equal graph cost in all five. A* expanded fewer nodes on three routes and the same number on two. In these short Editor queries, UCS happened to have lower measured milliseconds, so expanded-node count should not be presented as a guaranteed wall-time win.

The full-suite Main integration test and sandbox attack test failed in two slow Editor runs with 0.333 s `deltaTime` and realtime-based timeouts. Subsequent targeted tests passed after the disconnected patrol fallback and narrow test corrections: `main-integration-passed-20261125.xml` and `attack-sandbox-passed-20261125.xml`. The full suite has not been rerun on those final files, per the user's instruction to stop lengthy testing.
