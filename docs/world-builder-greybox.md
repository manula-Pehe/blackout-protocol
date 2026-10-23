# World Builder greybox

## Generate Main

1. Open this repository in Unity Hub using **6000.3.25f1**. Let the existing packages resolve and scripts compile. Do not run any teammate sandbox rebuild command.
2. Save or discard unsaved scene changes; exit Play Mode. The builder refuses to run while any loaded scene is dirty.
3. Select **Tools > Blackout Protocol > Build Greybox**. The first run accepts only the exact approved saved `Assets/_Project/Scenes/Main.unity`: Main Camera, Directional Light, Environment at (0,3,0), and its empty Transform-only child `01_LoadingBay` with identity local transform. A saved-file hash and loaded hierarchy/component checks reject unexpected content, extra children, changed transforms and prefab roots. Even a resaved scene with different serialization is refused; do not bypass this check.
4. The builder opens Main additively only if needed, then extends that same scene in place and saves it. It never creates a replacement scene. Existing Environment/01_LoadingBay objects, names, transforms and references remain intact. Main Camera and its components are retained; only its Camera and AudioListener are disabled so the existing Player prefab can supply FPS viewing/audio. Directional Light and scene render settings are preserved. Unrelated scenes remain open; close them before baking or playing.
5. Inspect the Console. Successful generation reports 15 cover points, four enemy markers, and two Hub/Core routes. Enter Play Mode to walk the layout using the existing Player prefab controls.

Generated materials and a SHA-256 generation receipt live under `Assets/_Project/Materials/WorldGreybox/`. Re-running against the unchanged saved scene is a no-op: it does not duplicate objects or assets. After manual edits, baking, prefab updates saved into Main, or other scene changes, generation refuses to overwrite Main. The receipt is an overwrite guard, not a backup or an asset-integrity validator. Keep all generated assets and Unity-created `.meta` files together. Scene additions/component changes are grouped into one Undo operation; generation errors before saving roll those scene edits back. Generated material assets are retained for inspection on failure, and the reserved output folder prevents retries from duplicating them. A saved scene is retained if receipt creation fails afterward; further generation is refused. There is no force-rebuild command.

## Layout and navigation

All dimensions and placement coordinates are world-space metres; floor tops are at Y=0, cover markers at Y=0 and the player spawn at Y=1.1 despite Environment remaining at Y=3. New groups retain world-origin transforms when parented, while primitive/marker positions are explicitly assigned in world space. Loading Bay geometry is added directly under the existing `Environment/01_LoadingBay`; its floor centre has local Y=-3.25 and world Y=-0.25. Other zones, corridors and spawns live under `Environment/Blackout Protocol - Generated Greybox v1`. The four rooms have distinct names, floor labels, accent stripes and lights. Geometry uses primitive cubes with BoxColliders. There are no ceilings, locked doors, scripted objectives, or AI.

| Space | Centre X,Z | Size X,Z | Cover points |
|---|---|---|---|
| Loading Bay | -24,0 | 20,18 | 3 |
| Security Hub | 0,0 | 24,24 | 6 |
| Maintenance / Storage | 12,30 | 32,16 | 3 |
| Control Core | 30,0 | 20,24 | 3 |

Loading connects east to Hub. Hub connects east directly to Core, and north to Maintenance. Maintenance connects south to Core via a separate corridor at X=26. Passage openings are 4 m wide; corridor wall thickness leaves 3.6 m clear width, with 4 m tall walls and no overhead blockers. Covers leave the route entrances clear. Cover markers sit 1.3 m behind block centres (0.7 m from the block face) and face +Z, toward the protecting block/threat. They reuse the existing `CoverPoint` component with its default 1.5 m cover height. The existing Player prefab is instantiated at (-30,1.1,0), facing east; its source asset is never edited.

Layer assignments use the existing Core constants and validate the project layer names before changing anything: Ground=10 for floors/stripes, Obstacle=9 for walls/cover/core landmark, Enemy=7 for empty enemy spawn markers, Interactable=8 for empty future console/exit placement markers, and Player=6 for the Player instance hierarchy. Markers have no colliders and no AI. No interaction prefabs are instantiated, so unfinished layer/reference wiring cannot block traversal. Floors, walls and cover are marked static for batching/occlusion; the modern surface explicitly collects collider geometry and does not require the obsolete Navigation Static flag.

## Bake NavMesh

1. Open only Main, outside Play Mode. Select the existing **Environment** object, which now has the NavMesh Surface. It collects both the existing Loading Bay branch and the other generated geometry. Do not move Environment to Y=0.
2. Inspect its **NavMesh Surface** component: Agent Type **Humanoid** (ID 0), Collect Objects **Current Object Hierarchy / Children**, Use Geometry **Physics Colliders**, Include Layers **Ground + Obstacle**. UI wording can vary by package patch.
3. In **Window > AI > Navigation**, confirm Humanoid radius **0.5**, height **2**, slope **45**, step height **0.75** against the current project defaults. This builder does not modify shared agent settings.
4. Click **Bake** on the surface component. Save Main with **Cmd+S**. Keep the generated navigation data and its `.meta` alongside the scene if Unity creates a new asset folder.
5. Enable navigation visualization and check a continuous blue surface through Loading→Hub→Core and Hub→Maintenance→Core. Cover marker feet and spawn points must lie over the walkable floor. Check that walls/cover cut out the surface. Move/jump around both routes in Play Mode; verify there are no collider gaps or blocked entrances.

The surface uses the already-declared **com.unity.ai.navigation 2.0.14** API (`NavMeshSurface`, `CollectObjects.Children`, `NavMeshCollectGeometry.PhysicsColliders`). No ProBuilder API or manual operation is used. Do not use the legacy scene-wide bake in place of this surface. NavMesh is intentionally not baked by the builder; player traversal uses CharacterController physics and works independently of a bake. Baked surface connectivity, lighting, label rendering and prefab input bindings require validation inside Unity. TextMesh explicitly uses Unity 6's built-in `LegacyRuntime.ttf` font/material; no TMP font import is required.

After a bake or manual scene changes, the builder's receipt deliberately no longer matches Main and rebuilding stops. Continue ordinary World Builder editing and rebake as necessary. For a future deliberate regeneration, first preserve authored work and obtain explicit approval to replace it; do not delete the receipt as a shortcut.

No shared contracts, teammate scripts/prefabs/sandboxes, build settings, AI, game flow, or package manifests are modified by this feature. The pre-existing local ProBuilder package changes are unrelated.
