# Plasma Rifle Model Evidence

## Assets

- Blender source: `Assets/_Project/Models/Source/PlasmaRifle.blend`
- Unity export: `Assets/_Project/Models/Export/PlasmaRifle.fbx`
- Unity prefab: `Assets/_Project/Prefabs/Weapons/PlasmaRifle.prefab`
- Sandbox scene: `Assets/_Project/Scenes/Sandbox/Ashen/PlasmaRifleImportCheck.unity`
- Material: `Assets/_Project/Materials/Models/Plasma Rifle - Atlas.mat`

## Geometry and Unity import

- Blender source triangle count: **3,756**.
- Unity imported triangle count: **3,708**; both counts are below the 5,000-triangle brief limit.
- Mesh parts: **45**; all have the packed `PlasmaRifle_AtlasUV` map.
- No n-gons remain in the Blender source after triangulation.
- The `Magazine - removable assembly` remains a separate named hierarchy group.
- The `Muzzle` empty remains in the imported hierarchy.
- FBX importer scale: **1**; Read/Write: **off**; hierarchy preservation: **on**; animation import: **off**.
- The prefab uses two simple `BoxCollider`s (body and magazine) and no `MeshCollider`s.

## Textures and material

- Albedo atlas: 1024 × 1024 PNG, sRGB.
- Emission atlas: 1024 × 1024 PNG, sRGB; cyan coil and indicator surfaces emit.
- Shader: URP Lit; Metallic: **0.58**; Smoothness: **0.58**; emission enabled.

## Modeling screenshots

Wireframe with Blender mesh statistics:

![Plasma rifle wireframe and triangle count](PlasmaRifle_Wireframe.png)

Packed UV layout:

![Plasma rifle UV layout](PlasmaRifle_UV_Layout.png)

![Plasma rifle albedo atlas](../../../Assets/_Project/Textures/Models/PlasmaRifle_Albedo.png)

![Plasma rifle emission atlas](../../../Assets/_Project/Textures/Models/PlasmaRifle_Emission.png)
