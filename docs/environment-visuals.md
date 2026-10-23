# Environment visual pass

Run **Tools > Blackout Protocol > Apply Environment Visuals** in Unity **6000.3.25f1**, outside Play/Prefab Mode, with **Main.unity open alone and active**. Save existing scene edits before the first application. The command does not open, rebuild or automatically save a scene. Inspect the result, then save Main with **Cmd+S**. Repeating the command on the same styled environment is a no-op, including before saving its visual changes.

## Treatment

### Lighting correction for an already-styled v1 scene

Use the **same menu command** after saving Main outside Play Mode. The command recognizes the exact original v1 lighting and upgrades its global fill: flat ambient RGB changes from `(0.32, 0.35, 0.40)` to `(0.65, 0.69, 0.75)`; the existing directional light changes from intensity `0.18` / RGB `(0.56, 0.67, 0.80)` to `0.65` / `(0.82, 0.88, 1.00)`. Omnidirectional ambient fill supports corridor/shadow-side readability while the existing colored zone lights, dark sky and emissive accents retain the industrial palette. Final Game-view brightness still requires visual verification.

The upgrade reuses every material and light; it creates no assets and changes no material assignments. It accepts only complete legacy or corrected lighting states, not mixed/custom lighting. Rerunning corrected visuals is a no-op. A single Undo restores v1 lighting. The v1 asset manifest remains unchanged: only the exact inspected saved `Night-Sky.mat` variant is additionally recognized by a filename + original hash + saved hash allowlist; all other asset guards remain strict. No general reimport bypass is provided. Do not rebake or regenerate the environment.

| Area | Visual treatment |
|---|---|
| Loading Bay | Warm industrial gunmetal, cargo hazard bands, red emergency lighting and emissive red identification strip |
| Security Hub | Cold blue steel, panelled floor/walls, blue/cyan practical light and emissive strip |
| Maintenance / Storage | Weathered olive/amber steel, opaque grating-pattern floor, amber practical light and hazard-striped cargo |
| Control Core | Pale alloy walls, dark teal panels, cyan-green lighting and emissive reactor panels |

The existing four zone lights and directional light receive deterministic color/intensity overrides. A dark procedural sky, flat readable ambient fill and reduced reflection intensity replace the outdoor daylight appearance. No extra lights, meshes, colliders, runtime effects or scene components are added. Procedural panel/grating/cargo textures are deterministic 128² opaque textures with mipmaps; they do not create physical holes or alter walkability. Floor tiling is scaled to each existing room/corridor. Existing zone labels remain intact.

The current PC URP asset supports HDR and additional lights, and its renderer includes SSAO. The Player camera currently has post-processing disabled. This pass does **not** modify that camera, URP assets, renderer features, shared materials or volume profiles. Emissive strips/cores remain visible without bloom; they do not imply bloom halos or baked illumination. The existing point lights provide actual local lighting. Both PC and Mobile URP configurations should be inspected in-engine for readability; material roughness and lighting may differ by renderer/quality level.

## Preservation and safety

- Only the known environment's **59 box MeshRenderers**, **four zone lights**, **original directional light**, and the scene's **skybox/ambient/reflection intensity** are styled. Actor roots implementing `IPlayerState` or `IDamageable` are excluded. Main currently contains the Player and Flanker; neither is restyled or changed.
- Named group/role counts, primitive meshes, component sets, source materials and a geometry-only fingerprint validate the inspected greybox. The fingerprint covers environmental world positions, scales, rotations and layers, not the entire Main file: actor placements and existing navigation-component changes do not invalidate the layout check. Geometry changes deliberately stop the pass rather than being reset. Environment stays at Y=3; floors remain at world Y=0.
- Components, GameObjects, transforms, colliders, mesh references, cover points and gameplay scripts are snapshotted before/after application. A mismatch rolls back scene overrides through one Undo group. Existing NavMeshSurface fields/references and the referenced baked asset/meta file hashes must remain unchanged. The duplicate surface setup is preserved as found; no navigation data is created, moved, reassigned or rebaked.
- Original material assets in `WorldGreybox`, models and enemy prefabs are never edited. New assets live only in `Assets/_Project/Materials/EnvironmentVisualsV1/`: 22 materials (including the sky) and three textures. Its ownership manifest fingerprints those assets and their metadata. Missing, manually edited, reimported or unexpected files cause a safe refusal; partial asset creation is retained for inspection, never deleted or overwritten automatically.
- On a first pass the expected original lighting/material references must still be present. A partially styled environment, unknown authored scene root/object, modified source lighting or later edits to this pass's assets/lighting stops application. There is no force mode. Do not reset Main or remove the manifest to bypass a refusal.
- The scene changes are one Undo operation; generated asset files remain if the scene operation is undone. Reapplying after a complete Undo is supported when original lighting/material assignments are restored. If you deliberately revise the layout or this visual recipe, review/update its guard and ownership version rather than overwriting existing output.

## Verify in Unity

1. Let scripts compile, open Main alone, save pending edits, then execute **Apply Environment Visuals**. Check Console success/refusal messages; pink materials or a refusal are failures to investigate, not reasons to rebuild the greybox.
2. Check Scene and Game views in all four rooms and along both Hub-to-Core routes. Confirm readable labels, distinct palettes, visible hazard bands, visible panel/grating detail and bright accents without a bloom dependency. There should still be only four zone point lights plus the original directional light.
3. Confirm Player/Flanker appearance, transforms, cover markers, colliders, layers and NavMeshSurface settings/data references are unchanged. Run **Tools > Blackout Protocol > Audit Navigation** before and after; require the same 12/12 complete zone paths and 4/4 direct corridor crossings. Do **not** rebake for this visual-only pass.
4. Save Main, rerun **Apply Environment Visuals**, and require the **already applied / no state changed** message. Undo the scene pass if the first result is undesirable; inspect the diff before retaining it. Preserve existing baked assets and gameplay edits.

The command and procedural assets require in-engine visual QA. Static compilation and file-preservation checks cannot establish final brightness, sky appearance, shader inclusion, material importer behavior or frame cost. Execution is intentionally left to the user.
