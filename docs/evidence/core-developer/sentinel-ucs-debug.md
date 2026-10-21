# Sentinel A* vs UCS and Debug View

## How to inspect the runtime diagnostics

1. Open `Assets/_Project/Scenes/Sandbox/Ashen/Sentinel_TestingGround.unity` and enter Play Mode.
2. Press **F1** to open the Sentinel diagnostics panel. It reports the HFSM path, player visibility, last known position, active destination and the latest scored patrol, observation or search candidates.
3. When the Sentinel has an active destination, click **Compare current destination**. This runs A* and UCS from the same sampled endpoints on the same graph. It can compare graph routes even when normal movement can take a direct NavMesh shortcut.
4. Read each algorithm's route cost, expanded-node count and graph-search elapsed time. Repeat the comparison to see how timings vary; one short timing is not a stable benchmark.
5. Select the Sentinel in the Scene view to see its vision cone, decision candidates and the graph paths (cyan for A*, orange for UCS).

The route cost is measured between graph anchors and omits the endpoint-to-anchor legs. The time is for the graph search only. The comparison is a diagnostic run and does not replace the production A* movement path.

## Evidence capture

**Pending:** Capture the Unity Game view with the debug panel showing both successful searches and the Scene view with the cone/candidate/path gizmos visible. Record the platform and include the actual values shown by the panel. No runtime comparison result is claimed by this note yet.
