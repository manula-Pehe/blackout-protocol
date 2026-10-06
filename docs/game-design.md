# Blackout Protocol — Game Design Document

## 1. Overview

**Genre:** First-person shooter with autonomous AI enemies
**Engine:** Unity 6 LTS (URP)
**Setting:** An underground robotics research facility during an emergency power failure
**Visual style:** Low-poly sci-fi, dark emergency lighting

Deep underground, a robotics research facility suffers a sudden power failure. The main power is out, but the security system switches to emergency mode and loses control: every robot in the facility now treats anyone inside as an intruder.

The player is a maintenance technician trapped inside. To survive, they must fight through the facility, reach the **Control Core**, shut down the security system, and escape through the **service elevator**.

---

## 2. Core Gameplay Loop

1. **Start:** the player begins in the Loading Bay with a plasma rifle, in near darkness lit by red emergency lights.
2. **Learn:** the first area introduces movement, shooting and doors against a single enemy.
3. **Progress:** each zone introduces a new enemy type, so each behaviour can be understood on its own.
4. **Escalate:** later zones combine enemy types, which begin to coordinate (e.g. the drone's alarm drawing others in).
5. **Finale:** the player reaches the Control Core, activates the shutdown console, and runs for the elevator.

A full playthrough takes about **5–8 minutes**.

**Win:** activate the shutdown console, then reach the elevator.
**Lose:** the player's health reaches zero; the game restarts from the Loading Bay.

---

## 3. The Player

| Mechanic | Description |
|---|---|
| **View** | First-person; the rifle is visible at the bottom of the screen |
| **Movement** | Walk, run, crouch and jump, with gravity and collisions (WASD + mouse) |
| **Shooting** | Raycast from the screen centre; limited ammo with reloading |
| **Interaction** | **E** to open doors, use switches and pick up objects; push crates by walking into them; throw held objects with the mouse |
| **HUD** | Health bar, ammo count, crosshair |
| **Noise** | Shooting, running and thrown objects emit noise that some enemies can hear; crouching is quiet |

---

## 4. Level Design — 4 Zones

```
[1 Loading Bay]──door──[2 Security Hub]──────door──────[4 Control Core]──[Elevator EXIT]
    (start)                  │                               │
                         barricade                          vents
                             │                               │
                     [3 Maintenance / Storage]───────────────┘
```

| Zone | Look | Gameplay purpose |
|---|---|---|
| **1. Loading Bay** | Cargo containers, loading ramp, red emergency lights | Safe start and introduction; one enemy |
| **2. Security Hub** | Large open room with desks, server racks, pillars, blinking monitors | Main combat space with plenty of cover and two routes onward |
| **3. Maintenance / Storage** | Narrow aisles, shelves, crates, pipes, metal grating floor | Tight, tense space; crates, barricades and throwables; suited to stealth |
| **4. Control Core** | Tall chamber with a glowing power core and the shutdown console | Final encounter and objective; leads to the elevator |

**Design principles:**
- **Two routes** from the Security Hub to the Control Core (direct door, or Maintenance and vents) let enemies choose paths and flank.
- A **movable barricade** between the Hub and Maintenance can open or block a route, forcing enemies to re-plan.
- Each zone has a distinct spatial character (open, tight, tall), giving each enemy type a space where its behaviour stands out.
- **Cover points** behind desks, pillars and crates are marked in the level for AI use.
- Pacing alternates between calm and combat, introducing enemy types one at a time before combining them.

---

## 5. Interactive Objects

| Object | Behaviour | Gameplay role |
|---|---|---|
| **Sliding blast doors** | Open/close with E; some locked until a switch is used | Control routes between zones; closed doors change enemy paths and muffle sound |
| **Door switches** | Wall panels that unlock doors | Objectives and locked shortcuts |
| **Cargo crates** | Physics objects pushed by walking into them | Create cover or block corridors |
| **Barricades** | Heavy movable blockers on certain routes | Open or close paths; enemies re-plan |
| **Throwable canisters** | Picked up and thrown; bounce and clatter | Physics interaction; noise distracts sound-sensitive enemies |
| **Shutdown console** | Activated with E | Final objective |

Whenever a door or barricade changes state, a level-changed event is broadcast so all agents can update their plans.

---

## 6. Enemies

All enemies are security robots. Three are humanoid androids built on a shared robot base, distinguished by accent colour and scale; one is a custom-modelled flying drone. Each enemy uses a **different decision-making architecture** and its **own pathfinding** built on the baked NavMesh.

### 6.1 Hunter Bot — Utility AI
- **Concept:** a maintenance robot whose cameras failed in the blackout. Blind, it hunts entirely by sound.
- **Behaviour:** wanders while listening; investigates gunshots, footsteps and impacts; moves faster and more aggressively as noise grows louder and more recent; searches when the trail goes cold, then resumes wandering.
- **Sound model:** noise propagates through walkable space, so sound behind a wall is perceived as coming from the nearest opening. Closed doors attenuate sound. The Hunter becomes less responsive to noise types that repeatedly lead nowhere (e.g. thrown decoys).
- **Decision-making:** Utility AI scores Wander, Investigate, Chase, Search and Attack each update, using response curves over loudness, recency, distance and trust, with hysteresis to prevent oscillation.
- **Accent:** yellow.

### 6.2 Flanker Android — Behaviour Tree
- **Concept:** a combat security unit trained in tactical engagement.
- **Behaviour:** moves between cover points and circles to attack from the side or behind. Adapts to the player's state: rushes when the player is reloading or at low health, holds cover when being aimed at, retreats when badly damaged.
- **Pathfinding:** prefers routes outside the player's line of sight.
- **Decision-making:** Behaviour Tree evaluating player vulnerability, threat, cover quality and flanking opportunities.
- **Accent:** blue.

### 6.3 Breacher Unit — GOAP
- **Concept:** a heavy riot-control robot built to get through obstacles.
- **Behaviour:** when a door or barricade blocks the way, plans a sequence of actions to get through, or chooses a cheaper alternative route.
- **Decision-making:** Goal-Oriented Action Planning with the goal "reach and attack the player" and actions Move, OpenDoor, PushBarricade, Attack and TakeCover. Re-plans when doors or barricades change.
- **Accent:** red, larger scale.

### 6.4 Sentinel Drone — Hierarchical State Machine
- **Concept:** a flying security camera drone still running its patrol routine.
- **Behaviour:** patrols, raises an alarm on spotting the player (alerting nearby robots to the player's position), observes from a safe distance, and searches intelligently when contact is lost.
- **Decision-making:** Hierarchical State Machine (Patrol, Alert, Track, Search, Return) with scored waypoint selection, observation-point selection, and predictive search from the player's last known position and heading.
- **Movement:** follows ground-level paths at a fixed hover height.
- **Model:** custom-built in Blender.

### 6.5 Enemy Interplay
- The Sentinel's alarm informs other robots of the player's position.
- Combat noise attracts the Hunter.
- The player's reload and health state influence the Flanker.
- Doors and barricades moved by the player or the Breacher change everyone's paths.

---

## 7. Technical Architecture — Brain and Body

```
Game events (noise, doors, alarms)
        │
        ▼
┌─────────────── Agent brain (per enemy) ───────────────┐
│ Perception → Memory → Decision-making → Pathfinding   │
└───────────────────────────┬───────────────────────────┘
                            │ MoveCommand (path, style, look target)
                            ▼
┌──────────────── Agent body (shared) ──────────────────┐
│ Path following → Steering → Rotation → Animation      │
└────────────────────────────────────────────────────────┘
```

- **Brains** decide what to do and compute a path through the level. They never access animation, rendering or rotation directly.
- **The body** executes paths: smooth path following with arrival and cornering, smooth rotation (including strafing while facing a target), and animation blending (idle, walk, run, strafe, attack, hit, death). The drone uses procedural hover motion.
- **Pathfinding** for every agent is built on the baked NavMesh as its navigation graph.
- **Shared contracts** in `Scripts/Core/` define how brains, bodies and game systems communicate.

This separation keeps AI logic independent of visuals, testable in isolation, and reusable across all four enemy types.

---

## 8. Custom 3D Models

| Model | Rationale |
|---|---|
| **Sentinel drone** | Hard-surface design suited to clean topology; no skeletal rig required; highly visible as an active enemy |
| **Plasma rifle** | On screen throughout play as the first-person view model |
| **Shutdown console / power core** *(optional)* | Centrepiece of the final room |

All custom models are low-poly (target under ~5,000 triangles), UV-mapped and textured to match the art direction.

---

## 9. Art Direction

- **Style:** low-poly sci-fi with simple forms and clean surfaces.
- **Palette:**
  - Gunmetal grey — walls and floors
  - Cyan — robot glows, screens, power core
  - Red — emergency and alarm lighting
  - Amber — warning signage and hazard stripes
- **Lighting:** main power is out; mostly baked dim emergency lighting, rotating alarm lights, flickering lamps and the glowing core.
- **Atmosphere:** light fog, sparks from damaged panels, blinking monitors.
- **Enemy readability:** shared robot base with distinct accent colours and scale per type.

---

## 10. System Interactions

```
Player ──shoots / runs / throws──►  Noise events  ──►  Hunter reacts
   │
   ├──reloads / loses health──►  Player state  ──►  Flanker adapts tactics
   │
   └──opens doors / pushes barricades──►  Level changed  ──►  All agents re-plan

Sentinel spots player ──►  Alarm  ──►  Flanker + Breacher converge

Baked NavMesh ──►  Agent pathfinding  ──►  Agent decision-making
Agent brain ──MoveCommand──►  Agent body  ──►  Movement + animation
```

---

## 11. Team Responsibilities

| Member | Graphics responsibility | AI agent | Additional responsibilities |
|---|---|---|---|
| **Mahima** | World Builder — level layout, lighting, texturing, NavMesh | Flanker android (Behaviour Tree) | Game flow (objective, win/lose, restart), ambient and alarm audio |
| **Chethiya** | Systems Engineer — player controller and interaction physics (doors, switches, crates, barricades, throwables) | Breacher unit (GOAP) | Interaction sounds |
| **Ashen** | Core Developer — custom 3D models, topology, UV mapping | Sentinel drone (Hierarchical State Machine) | Player shooting, ammo/reload, player health, HUD, enemy visual variants |
| **Manula** | Agent Controller — agent movement, rotation and animation along computed paths | Hunter bot (Utility AI) | Shared agent contracts, enemy health and death handling |

### 11.1 Mahima — World Builder
- Top-down zone layout, greybox (ProBuilder), cover and interaction placement, cover-point markup
- Textures and materials in the agreed palette; baked emergency and alarm lighting, fog
- NavMesh baking and re-baking after layout changes; main scene ownership and integration

### 11.2 Chethiya — Systems Engineer
- Player controller: movement, gravity, jumping, crouching, collisions
- Sliding doors, switches, shutdown console, pushable crates and barricades, pick-up and throw
- Broadcasting level-changed and impact-noise events

### 11.3 Ashen — Core Developer
- Sentinel drone and plasma rifle modelled from scratch (optional: console / power core)
- UV unwrapping, texturing, FBX export and import with correct scale and materials
- Raycast shooting, ammo and reload, player health, HUD, enemy accent variants

### 11.4 Manula — Agent Controller
- Shared agent contracts (`MoveCommand`, agent body interface, noise events)
- Path following with arrival and cornering, path replacement mid-route, stuck detection, ground snapping, agent separation
- Smooth rotation and strafing; Mixamo robot setup; Animator blend trees; attack, hit and death states; drone hover motion
- Debug visualisation of paths

### 11.5 Team Conventions
- Each member commits their own work from their own GitHub account (see `CONTRIBUTING.md`).
- Each member records design decisions in `docs/decision-logs/`.
- Each member places their agent's spawn points and patrol routes.
- The full game is integrated and play-tested together regularly.
