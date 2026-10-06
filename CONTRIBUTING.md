# Contributing

## Principles

- Each member commits only their own work.
- Commits are small and focused — one logical change per commit.
- `main` and `develop` are updated only through reviewed pull requests.
- `Scenes/Main.unity` is owned by the World Builder; other members work in their sandbox scenes and deliver prefabs.
- Contracts in `Scripts/Core/` are changed only by team agreement.

## Branching

| Branch | Purpose |
|---|---|
| `main` | Stable, playable builds; tagged releases (`v0.1-prototype`, `v1.0`) |
| `develop` | Integration branch |
| `feature/<area>-<description>` | New work, e.g. `feature/agentbody-path-follow` |
| `fix/<description>` | Bug fixes |

## Commit Messages

`type(area): summary` in the imperative mood.

| Type | Scope |
|---|---|
| `feat` | New feature or behaviour |
| `fix` | Bug fix |
| `refactor` | Restructuring without behaviour change |
| `perf` | Performance improvement, with the measured result |
| `test` | Tests |
| `art` | Models, textures, materials, lighting, animation assets |
| `docs` | Documentation and decision records |
| `chore` | Configuration, packages, tooling |

Examples:
- `feat(interaction): add pushable barricade with rigidbody constraints`
- `perf(agentbody): throttle path recalculation to 4 Hz (AI frame time 1.8 ms → 0.4 ms)`
- `art(models): unwrap drone body UVs and fix seams`

## Ownership

| Area | Owner |
|---|---|
| `Scenes/Main.unity`, `Materials/`, `Textures/`, `Lighting/` | World Builder |
| `Scripts/Player/`, `Scripts/Interaction/` | Systems Engineer |
| `Models/` | Core Developer |
| `Scripts/AgentBody/`, `Animations/` | Agent Controller |
| `Scripts/AI/<Agent>/`, `Tests/EditMode/<Agent>/` | Agent owner |
| `Scenes/Sandbox/<Member>/` | Individual member |
| `Scripts/Core/` | Shared |

## Code Standards

- AI logic (pathfinding, decision-making, perception) is written as plain C# classes, independent of `MonoBehaviour` where practical, and covered by EditMode tests.
- Agents communicate with the rest of the game only through the contracts in `Scripts/Core/`.
- No per-frame heap allocations in `Update` paths; reuse buffers and cache component and Animator parameter lookups.
- Public types and non-obvious logic carry XML documentation comments.

## Assets

- Binary assets are tracked with Git LFS (see `.gitattributes`).
- Every asset is committed together with its `.meta` file.
- Texture budget: 1024² by default, 2048² for large surfaces only.
- Third-party assets are placed in `ThirdParty/` and recorded in `CREDITS.md` in the same commit.
- Video recordings and unused asset packs are not committed.

## Design Records

Significant design and optimisation decisions are recorded in `docs/decision-logs/` using `TEMPLATE.md`. Performance measurements supporting those decisions are stored in `docs/evidence/`.