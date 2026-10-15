# Sentinel Drone Model Evidence

Source reviewed: `Assets/_Project/Models/Source/SentinelDrone.blend`  
Unity export: `Assets/_Project/Models/Export/SentinelDrone.fbx`  
Unity prefab: `Assets/_Project/Prefabs/AI/Sentinel/SentinelDrone.prefab`

## Geometry and scale

- Blender unit system: Metric, scale length `1.0`, unit `1 m`.
- Mesh objects: `53`; all `53` have a UV map.
- Total triangles: **3,692**, within the brief's 4,000-triangle drone budget.
- Overall Blender dimensions: approximately **0.817 × 0.646 × 0.327 m**.
- Unity FBX importer scale: `1`; Read/Write: off; Mesh Compression: off.
- The Unity prefab has one simple `BoxCollider` sized approximately **0.808 × 0.628 × 0.345 m**. It has no `MeshCollider`.
- The drone's four rotor pivots and camera aim pivot remain separate in the imported hierarchy.

The source contains 18 polygon faces with more than four sides: two 10-sided faces on the antenna mast and sixteen 12-sided faces on the four rotor housings and four rotor hubs (two caps per part). These are planar end caps; review them in Blender before final topology sign-off.

## Wireframe view

![Sentinel Drone wireframe view](SentinelDrone_Wireframe.png)

## UV layout

This layout exports the UV coordinates from all 53 meshes against the 2:1 texture-atlas aspect ratio. Repeated parts produce overlapping/stacked islands in places; check that these overlaps are intentional for the shared atlas.

![Sentinel Drone UV layout](SentinelDrone_UV_Layout.png)

## Unity texture and material settings

| Asset | Source dimensions | sRGB | Mipmaps | Read/Write | Max Size | Compression |
|---|---:|---|---|---|---:|---|
| `SentinelDrone_Albedo.png` | 1024 × 512 | On | On | Off | 2048 | Enabled, quality 50, crunch off |
| `SentinelDrone_Emission.png` | 1024 × 512 | On | On | Off | 2048 | Enabled, quality 50, crunch off |

The prefab's 53 mesh renderers reference `Sentinel Drone - Atlas.mat`, which points to the albedo and emission textures. The sandbox scene is `Assets/_Project/Scenes/Sandbox/Ashen/SentinelDroneImportCheck.unity`; it now uses the prefab instance.
