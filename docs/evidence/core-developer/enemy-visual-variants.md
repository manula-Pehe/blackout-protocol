# Enemy Visual Variants

The variants use Manula's `TitanVisual` rig and locomotion controller. Each prefab overrides the shared renderer material and retains the base normal and metallic maps.

| Enemy | Accent | Scale | Prefab | Albedo |
|---|---|---:|---|---|
| Hunter | Yellow | 1.0 | `Assets/_Project/Prefabs/Enemies/Hunter.prefab` | Titan base albedo |
| Flanker | Blue | 1.0 | `Assets/_Project/Prefabs/Enemies/Flanker.prefab` | `Assets/_Project/Textures/Models/Titan_Flanker_Blue.png` |
| Breacher | Red | 1.2 | `Assets/_Project/Prefabs/Enemies/Breacher.prefab` | `Assets/_Project/Textures/Models/Titan_Breacher_Red.png` |

The blue and red albedos are 1024 × 1024. Only yellow safety-panel pixels were hue-shifted; the dark armor, surface markings, and shading were retained. Both textures use sRGB, mipmaps, compression, and Read/Write off, with Max Size set to 1024.

Open `Assets/_Project/Scenes/Sandbox/Ashen/EnemyVisualVariantsCheck.unity` to compare the three prefabs side by side. The Hunter's yellow material uses the original Titan albedo, while the Flanker and Breacher use the recoloured copies.

The shared model source and attribution are recorded under `Assets/_Project/ThirdParty/AgentBody/Titan/` and in `CREDITS.md`.
