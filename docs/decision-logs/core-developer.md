# Decision Log — Core Developer / Ashen

## 2026-10-17 — Use a shared atlas material for the plasma rifle

**Context:** The rifle has many separate mesh objects that must remain individually selectable, including a removable magazine and muzzle marker. Assigning a different Unity material to each part would make the import harder to maintain.

**Options considered:**

1. Keep separate flat materials on each mesh part.
2. Pack the parts into one UV atlas and use one Unity material.

**Decision:** Use a 1024 × 1024 albedo atlas and a matching cyan emission atlas, with one URP Lit material. Keep the magazine meshes grouped under their removable assembly and retain the named `Muzzle` empty.

**Why:** This gives the model a consistent gunmetal, polymer, steel, cyan, and amber palette while keeping the Unity material setup straightforward. The UVs and hierarchy stay available for later reloading and muzzle effects.

**Evidence:** `docs/evidence/core-developer/plasma-rifle-evidence.md`.

## 2026-10-20 — Reuse the Titan rig for enemy visual variants

**Context:** The assignment calls for Hunter, Flanker, and Breacher variants of Manula's humanoid robot base, with different accent colours and a larger Breacher.

**Decision:** Keep the Titan base rig, animation controller, normal map, and metallic map shared. Use the original yellow albedo for Hunter, recolour only its yellow safety panels blue for Flanker and red for Breacher, and scale the Breacher variant to `1.2`.

**Why:** The variants stay recognizable as the same security-robot family while their colour and size make their roles easy to tell apart.

**Evidence:** `docs/evidence/core-developer/enemy-visual-variants.md` and `Assets/_Project/Scenes/Sandbox/Ashen/EnemyVisualVariantsCheck.unity`.
