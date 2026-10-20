# Titan locomotion source assets

Titan model by **Sotudios**, from the [Humanoid Robot Pack](https://sotudios.com/Assets/Humanoid-RobotPack/).
The publisher identifies the pack as [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/).
Credit Sotudios and retain this notice when reusing the model. No endorsement is implied.

The project owner supplied the model after Mixamo auto-rigging and three Mixamo
locomotion exports on 2026-10-20. Auto-rigging/animations: **Adobe / Mixamo**,
[service](https://www.mixamo.com/), [usage FAQ](https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html).
Mixamo usage terms and the original model license are separate. Game integration
does not grant permission to resell/repackage standalone animation source files;
review current terms before distributing raw project assets outside the team.

Original local source directory: `/home/manula/Downloads/BlackoutRobots/`.
The four FBX files are copied unchanged; original downloads are not edited.

| Filename | Purpose |
|---|---|
| `Titan_Rigged.fbx` | Skinned mesh, skeleton and generated Humanoid Avatar |
| `Titan_Idle.fbx` | Idle, exported with an unused duplicate skinned mesh |
| `Titan_Walk.fbx` | Walk animation |
| `Titan_Run.fbx` | Run animation |

Unity-only changes live in `.meta` files and separate AgentBody assets: import
scale correction, shared Avatar, looping/baked-root clip settings, URP material,
controller, visual prefab and sandbox. No FBX geometry or original rig is rewritten.

The subsequent visual-correction increment also uses the original pack extracted
locally at `/home/manula/Downloads/humanoid-robot-pack-v1.0.0/`:

- `characters/titan/textures/titan_basecolor.png`
- `characters/titan/textures/titan_normal.png`
- `characters/titan/textures/titan_roughness.png`
- `characters/titan/textures/titan_metallic.png`

Those four maps were already copied unchanged into
`Assets/_Project/Animations/AgentBody/Titan/Textures/` by the project owner; this
increment supplies their import metadata and material bindings. The original `LICENSE.md`
and `metadata/titan.json` are retained here as `LICENSE.md` and `titan.json`.
The only derived map is `TitanMetallicSmoothness.png` in the AgentBody animation
asset folder: red = metallic red, alpha = 255 minus roughness red, green/blue = 0.
The material uses the original maps and this URP channel packing. Run playback is
configured at 0.65 times the original rate for the existing 4 m/s body; animation
source data remains unchanged. No other pack characters were imported.
