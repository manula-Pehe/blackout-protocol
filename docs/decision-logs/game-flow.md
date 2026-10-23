# Decision Log — World Builder / Game Flow

## 2026-10-23 — Additive objective slice, logical elevator and scene reload

**Context:** Main contains a working Player/Flanker and baked navigation, but only empty console/elevator markers. Shared player, interaction and AI code must remain unchanged.

**Options considered:**
1. Install a physical exit door and modify existing player/HUD systems: larger integration surface and new navigation/layout risk.
2. Reuse ShutdownConsole, observe PlayerHealth, add a separate overlay and logical extraction region: smallest geometry-preserving demonstration.

**Decision:** Use a plain-C# state machine with one-way phases ReachCore → ActivateConsole → ExitUnlocked → Won, and death from any nonterminal phase → Lost. Resolve death before extraction in LateUpdate; terminal outcomes are immutable. Console activation checks player identity, health and Core bounds. The existing console prefab is instantiated under an owned group in the existing marker subtree; its layer-8 collider is trigger-only. Extraction uses a position bound at the elevator marker, not a physical door/trigger. A world-space exit sign changes on unlock; the overlay reads the existing PlayerInteractor prompt and coexists with PlayerCombatHud.

**Why:** No existing geometry, layers, cover points, materials, lights, navigation components or baked files need to change. Existing material assets are referenced, never edited; ShutdownConsole's existing runtime indicator uses its own material instance. The prefab and teammates' scripts are untouched. Game Flow runtime adapters stay in Assembly-CSharp to access existing player/interaction classes; pure logic and its EditMode tests have separate asmdefs.

**Terminal/restart policy:** Disable existing player gameplay/input components at runtime, retaining the camera, PlayerState and combat HUD; pause scaled simulation and unlock the cursor. R or gamepad Start reloads the saved Main scene exactly once. Restore previous time scale, component enabled flags and cursor state on teardown; failed loading remains paused and permits retry. Reload resets console, health, ammo, enemies and claimed cover through normal lifecycle teardown. No persistent singleton or shared GameEvents mutation is introduced. Restart returns to the saved Loading Bay spawn. Audio is intentionally optional/not added: no clips exist in the project.

**Setup safety:** Tools > Blackout Protocol > Setup Game Flow requires saved Main open alone, outside Play/Prefab Mode, exact empty markers at (37,0,-8)/(37,0,0), one existing compatible player, the inspected console prefab and referenced baked data. New objects live under `Spawn Markers - no AI/Blackout Protocol - Game Flow v1`; none are added to guarded room scopes or scene roots. A valid repeat is a no-op, including before saving; conflicting controllers, authored markers or modified owned setup cause refusal. Existing objects/components are serialized before/after (only the marker container's child list may grow); baked file/meta hashes must remain unchanged. Failures roll back created scene objects through Undo. Setup does not open/save/rebuild/rebake a scene or create/edit assets.

## Unity verification (execution left to the user)

1. Open the project in Unity 6000.3.25f1; allow compilation. Run EditMode tests under `BlackoutProtocol.GameFlow.Tests.EditMode`.
2. Open Main alone, exit Play/Prefab Mode, save pending edits, and run **Tools > Blackout Protocol > Audit Navigation**. Record its existing paths/crossings and surface warnings.
3. Run **Tools > Blackout Protocol > Setup Game Flow**. Require success. Check the existing marker transforms and geometry are unchanged. The new console is at world (37,1.2,-8), layer 8, with a trigger collider. The exit region is centered at (37,1,0), size (1.4,4,2.4); extraction needs no collider or Rigidbody. Save Main with Cmd+S. Rerun setup: require “already configured”. It must not duplicate anything. One Undo reverses the initial setup.
4. Play from Loading Bay. Visit the elevator before console activation: no win. Reach Core by either route, approach/aim at console within 3 m, verify the interaction prompt and press E. Require the objective/sign to change to unlocked, then enter the elevator region: Win. Controls must stop while the camera and HUD remain visible. Press R/Start; require a fresh Loading Bay spawn, full health, reset objective/console and normal time/input.
5. On a separate run let Flanker kill the player before/after console activation: require Lose once, no post-death movement/shooting/interactions and a working R/Start restart. Check same-frame death/extraction favors death when health is already zero. Exit Play Mode from the paused result; ensure time scale/input/cursor restore normally.
6. Rerun Audit Navigation: require unchanged results (target 12/12 zone paths and 4/4 corridor crossings), unchanged surface/data references and no baked-file diff. Do **not** rebake. Inspect Main's eventual integration diff: only the owned new group and the marker parent's child reference should be added; original geometry, Player/Flanker prefab overrides, lighting and navigation must remain unchanged.

**Limitations:** This is logical extraction, not a physical elevator/door animation. Console input inherits the existing layer-only raycast (it does not add a new wall-occlusion policy). Core bounds are tied to the current greybox. In-engine prefab serialization, HUD readability, pause/restart lifecycle and gameplay still require the above checks; static compilation and pure-state tests do not prove them.

**Static evidence:** All ten NUnit state cases passed via a temporary .NET runner without launching Unity. Logic, runtime adapters and Editor setup compiled against the installed Unity/project assemblies with warnings treated as errors. Unity EditMode discovery and scene integration remain unexecuted.
