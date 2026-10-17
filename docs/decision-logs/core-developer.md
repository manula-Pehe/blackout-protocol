# Decision Log — Core Developer / Ashen

## 2026-10-17 — Use a shared atlas material for the plasma rifle

**Context:** The rifle has many separate mesh objects that must remain individually selectable, including a removable magazine and muzzle marker. Assigning a different Unity material to each part would make the import harder to maintain.

**Options considered:**

1. Keep separate flat materials on each mesh part.
2. Pack the parts into one UV atlas and use one Unity material.

**Decision:** Use a 1024 × 1024 albedo atlas and a matching cyan emission atlas, with one URP Lit material. Keep the magazine meshes grouped under their removable assembly and retain the named `Muzzle` empty.

**Why:** This gives the model a consistent gunmetal, polymer, steel, cyan, and amber palette while keeping the Unity material setup straightforward. The UVs and hierarchy stay available for later reloading and muzzle effects.

**Evidence:** `docs/evidence/core-developer/plasma-rifle-evidence.md`.
