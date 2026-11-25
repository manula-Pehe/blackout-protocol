# Hunter viva evidence

Evidence in this folder is captured from the live Unity Editor using the official Unity MCP tools. `search-comparisons-*.csv` records equivalent A* and UCS queries on the same cached Main-scene graph; `main-gameplay-*.md` records natural action, noise, path and health observations. Screenshots show the F1/F3 overlay when the capture API supports it.

For a fair comparison, use `HunterNavMeshPathfinder.TryCompareSearches(start, goal, out aStar, out ucs)` or F3's comparison of the last actual route. Check both searches succeeded and that graph node count and endpoints match. Report total graph cost, expanded nodes and milliseconds separately. A nearby goal may give identical node counts, and a single millisecond timing is subject to Editor load and timer noise.

The Hunter's attack must be demonstrated by player-sourced acoustic evidence and normal Utility AI selection. A direct `AgentBody.PlayAction(Attack)` or direct animation-impact call is only an animation unit check, not natural combat evidence.

`main-integration-passed-20261125.xml` and `attack-sandbox-passed-20261125.xml` are the final targeted test results, each 1/1 passed after the Hunter patrol fallback and test corrections. The `*-before-*` and `*-postfix-*` XML files preserve the failing diagnostic runs. The two complete EditMode suites remain failing and are stored under `docs/evidence/agent-controller/`.
