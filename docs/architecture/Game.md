# Game Module Architecture (MakaMek.Core)

## Overview
The Game module orchestrates the core game loop, state, and player interactions for MakaMek. It defines the structure and flow of a game session, including phases, player management, command handling, initiative, turn order, combat resolution, piloting skill mechanics, damage tracking, and networked play. The design is modular, supporting both server and client perspectives, and extensible to accommodate new rules, phases, and commands.

---

## Main Components

### 1. Game Core Classes
- **BaseGame:** Abstract base for all game types. Manages players, phases, turns, command publishing, and the battle map. Provides observables for state changes and handles phase-based damage tracking reset.
- **ClientGame:** Client-side implementation. Receives and processes commands, maintains command log, manages local players, and synchronizes state with the server.
- **ServerGame:** Server-side implementation. Owns the authoritative game state, manages phase transitions, validates and applies commands, broadcasts updates, and coordinates piloting skill calculations.
- **IGame:** Interface for all game types, exposing state, player list, map, rule providers, and mechanics calculators.
- **GameManager/IGameManager:** Responsible for game lifecycle management, lobby initialization, server startup, network integration, and dependency injection of game mechanics.

### 2. Players
- **Player/IPlayer:** Represents a participant in the game, including their units and status. Supports status updates and player-specific logic.

### 3. Commands System
- **Commands:**
  - Defines all actions that can be taken in the game (e.g., move, attack, join, end turn).
  - Split into `Client` and `Server` commands, with a shared `IGameCommand` interface.
  - Supports formatting for UI/logs and is designed for serialization and transport.
- **Command Handling:**
  - Commands are published, transmitted, and handled by both client and server using infrastructure in `Services/Transport`.

### 4. Game Phases
- **Phases:**
  - The game progresses through a set of well-defined phases: Start → Deployment → Initiative → Movement → WeaponsAttack → WeaponAttackResolution → Heat → End
  - Each phase is represented by a class implementing `IGamePhase` (e.g., `StartPhase`, `MovementPhase`, `WeaponsAttackPhase`, `WeaponAttackResolutionPhase`).
  - **PhysicalAttackPhase:** Exists as a placeholder implementation but is not currently integrated into the standard game flow (planned feature).
  - `PhaseManager` coordinates phase transitions and enforces phase-specific rules.
  - Each phase transition triggers `ResetPhaseState()` on all units to reset damage tracking.

### 5. Turn & Initiative
- **InitiativeOrder:** Tracks and resolves player initiative rolls, including tie-breakers and roll history.
- **TurnOrder:** Determines the sequence in which players and units act within a turn, supporting complex initiative and multi-unit moves.
- **TurnStep:** Encapsulates a single player's action within the turn order.

### 6. Mechanics & Dice
- **Mechanics:**
  - Encapsulated in the `Mechanics` submodule, with calculators for to-hit resolution and breakdowns for modifiers.
  - **Piloting Skill System:** `IPilotingSkillCalculator` provides comprehensive PSR (Piloting Skill Roll) calculations with detailed modifier breakdowns for various conditions (gyro damage, actuator hits, heavy damage, etc.).
  - **Fall Processing:** `IFallProcessor` handles mech falling mechanics, including PSR requirements and fall damage calculations.
  - **Damage Tracking:** Phase-based damage tracking with `TotalPhaseDamage` property that accumulates damage during each phase and resets between phases.
- **Dice:**
  - Dice rolling is abstracted for both random and deterministic play, supporting testability and custom dice logic.

### 7. Factories
- **GameFactory/IGameFactory:** Abstract creation of game/server/client instances, supporting testability and custom scenarios.

---

## Relationships & Data Flow
- The game loop is driven by the server (`ServerGame`), which manages phase progression, command validation, and authoritative state.
- Clients (`ClientGame`) receive and submit commands, update local state, and synchronize with the server.
- Commands flow through the `CommandPublisher` and transport services, ensuring reliable delivery and consistent state.
- Players, units, and the battle map are managed centrally, with state changes propagated via observables and commands.
- Phase transitions automatically reset unit damage tracking and other phase-specific state.
- Piloting skill calculations are performed server-side and broadcast to clients for UI display.

---

## Extensibility Points
- **Phases:** Add new game phases by implementing `IGamePhase` and updating the `PhaseManager`.
- **Commands:** Add new command types for new actions or features.
- **Combat/Dice:** Add new calculators or dice logic for advanced mechanics.
- **Piloting Skills:** Extend `IPilotingSkillCalculator` for new PSR conditions or modifiers.
- **Factories:** Use or extend factories for scenario setup, testing, or custom transports.

---

## Summary
The Game module provides the backbone for MakaMek's game flow, supporting multiplayer, networked play, complex rules, and advanced mechanics like piloting skill rolls and damage tracking. Its architecture separates domain logic (game, phases, commands, mechanics) from infrastructure (transport, factories), ensuring maintainability and adaptability for future features and gameplay evolution.
