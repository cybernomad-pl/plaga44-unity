# PLAGA '44 - Unity VR Survival Game

**Platform:** Meta Quest 3 / 3S
**Engine:** Unity (URP + XR Interaction Toolkit)
**Genre:** VR Survival / Historical

## Overview

PLAGA '44 is a VR survival game set during the Warsaw Uprising of 1944. The player must survive in war-torn Warsaw, managing physiology (hunger, thirst, hypothermia), crafting supplies, navigating NPC threats (military patrols, civilians, animals), and experiencing immersive audio-visual effects driven by the character's physical state.

## Project Structure

```
Assets/
├── Config/          # JSON configuration files
├── Data/            # Item and crafting data
├── Scripts/
│   ├── Audio/       # Audio managers, heartbeat, breathing, stress effects
│   ├── Core/        # GameManager, GameMode, SceneTransitions
│   ├── DualMode/    # Heritage/NoEZUS dual-mode controller
│   ├── Emersion/    # Immersion effects (dehydration, hypothermia, vision)
│   ├── Environment/ # Environment manager, time-of-day effects
│   ├── GameState/   # Game state management
│   ├── Inventory/   # Inventory, crafting, loot, backpack systems
│   ├── NoEZUS/      # NoEZUS controller
│   ├── NPC/         # NPC behavior, threat assessment, encounters
│   ├── Physiology/  # Physiology controller (hunger, thirst, temperature)
│   ├── SaveSystem/  # Save/load, auto-save
│   ├── Survival/    # Seasonal survival, hydration, nutrition, shelter
│   ├── Terrain/     # Terrain generation and management
│   ├── UI/          # HUD, menus, status effects
│   └── XR/          # VR locomotion, XR interaction
└── ProjectSettings/ # Unity project settings
```

## Feature Branches

Each major system is developed on its own feature branch:

| Branch | System |
|--------|--------|
| `feature/unity-vr-project-structure` | Core VR project structure, GameManager, XR locomotion |
| `feature/audio-emersion-effects` | Immersion audio-visual effects (stress, tremor, vision) |
| `feature/audio-vr-emersion-system` | VR audio system (heartbeat, breathing, dehydration) |
| `feature/22-physiology-controller-system` | Physiology controller (hunger, thirst, temperature) |
| `feature/seasonal-survival-system` | Seasonal survival (weather, shelter, hydration, nutrition) |
| `feature/inventory-crafting-system` | Inventory and crafting system |
| `feature/npc-threat-system` | NPC threats (military, civilian, animal encounters) |
| `feature/save-load-ui-system` | Save/load system and UI (HUD, menus) |
| `feature/terrain-environment-system` | Terrain generation and environment effects |

## Development

This repository contains only the Unity C# source code and configuration files. It was extracted from the main [plaga44](https://github.com/cybernomad-pl/plaga44) repository which also contains the web application.

## License

All rights reserved. (c) cybernomad-pl
