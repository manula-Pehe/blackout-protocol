# Core Developer Branches

## GV

- `feature/ashen-sandbox-setup` — sets up Ashen's Unity sandbox scene for testing.
- `feature/combat-shooting` — adds the player shooting test scene for checking weapon behavior.
- `feature/combat-feedback` — adds shooting, reload, empty-magazine and hit feedback, with audio and HUD setup.
- `feature/player-combat-hud` — styles the player health and ammunition HUD.
- `feature/plasma-rifle-model` — creates and documents the plasma rifle model, UVs, textures and materials.
- `feature/model-sentinel-drone` — creates the Sentinel drone model and materials, with an import-check scene.
- `feature/sentinel-drone-prefab` — builds the Sentinel drone prefab and records its evidence.
- `docs/sentinel-drone-signoff` — records the Sentinel drone sign-off and supporting evidence.
- `feature/enemy-visual-variants` — creates the Hunter, Flanker and Breacher visual variants and comparison scene.
- `docs/core-developer-submission-evidence` — collects Core Developer evidence, including the combat Profiler capture.

## IS

- `feature/sentinel-drone-hsm` — implements the Sentinel's patrol, alert, tracking, search and return behavior.
- `feature/sentinel-editmode-tests` — adds EditMode coverage for Sentinel state transitions, decisions and pathfinding.
- `feature/sentinel-ucs-debug` — adds Sentinel A* and UCS comparison and decision diagnostics.
- `feature/sentinel-edge-cases` — handles Sentinel edge cases from section 7.6, including unreachable search points, repeated alarms, stuck movement, player death and being shot down.
- `feature/sentinel-main-integration`  — integrate the Sentinel into the Main scene and verify it in the project environment.
