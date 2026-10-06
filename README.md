# Blackout Protocol

**SE3032 Graphics & Visualization × SE3062 Intelligent Systems — Joint Group Project**

> A power failure hits an underground robotics research facility and its security robots go rogue. You play a maintenance technician who must reach the Control Core, shut down the security system, and escape. Full design: [docs/game-design.md](docs/game-design.md).

A first-person 3D shooting game where the player navigates a level, interacts with physics objects (doors, barricades, throwables) and fights four distinct AI-controlled enemies. Each team member owns one graphics role (GV) and one autonomous AI agent (IS).

## Team roles

| Role | Graphics responsibility (GV) | AI agent (IS) |
|---|---|---|
| S1 — World Builder | Level design, lighting, texturing, NavMesh bake | Own agent |
| S2 — Systems Engineer | Player interaction physics (doors, barricades, throwables) | Own agent |
| S3 — Core Developer | Custom 3D models (Blender), topology, UV mapping | Own agent |
| S4 — Agent Controller | Agent movement, rotation and animation along calculated paths | Own agent |

## Tech stack

- **Engine:** Unity 6 LTS — exact version pinned in `ProjectSettings/ProjectVersion.txt`
- **Render pipeline:** URP (Universal Render Pipeline)
- **Language:** C#
- **Modeling:** Blender
- **Characters / animations:** Mixamo
- **Version control:** Git + GitHub + Git LFS

## Requirements

- Unity version specified in `ProjectSettings/ProjectVersion.txt`
- Git LFS

See [CONTRIBUTING.md](CONTRIBUTING.md) for branching, commit and ownership conventions.

## Project structure

```
Assets/_Project/
  Scenes/                    Main scene + per-member sandbox scenes
  Scripts/Core/              Shared contracts between agents and game systems
  Scripts/Player/            Player controller, shooting, health
  Scripts/Interaction/       Doors, barricades, throwables (Systems Engineer)
  Scripts/AI/<AgentName>/    Each member's own AI agent
  Scripts/AgentBody/         Agent movement + animation (Agent Controller)
  Models/                    Custom Blender models: Source/ (.blend) + Export/ (.fbx)
  Materials/  Textures/  Lighting/   (World Builder)
  Prefabs/  Animations/  Audio/  UI/
  ThirdParty/                Downloaded assets (credited in CREDITS.md)
  Tests/EditMode/            Unit tests for AI logic
docs/
  game-design.md             Game design document
  decision-logs/             Design decision records per member
  evidence/                  Profiler captures and performance measurements
```

## Branches

- `main` — always a working, playable build (tagged at each release)
- `develop` — integration branch
- `feature/*`, `fix/*` — work branches merged into `develop`

## Credits

All third-party assets are listed in [CREDITS.md](CREDITS.md).