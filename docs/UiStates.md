﻿# UI States Module Architecture (MakaMek.Presentation)

## Overview
The `UiStates` module manages user interaction logic for different game phases. Each UI state encapsulates the rules, available actions, and input handling for a specific phase or context, acting as a bridge between the core game logic and the user interface (typically via ViewModels). This design enables clear separation of phase-specific UI behavior and supports extensible, maintainable interaction flows.

As part of the MakaMek.Presentation layer, UI States serve as an intermediary between the core game logic (MakaMek.Core) and user interactions in MakaMek.Avalonia.

---

## Main Components

### 1. IUiState
- **Purpose:** Interface for all UI state classes.
- **Responsibilities:**
  - Exposes the label for the current action and whether user input is required.
  - **Primary Action Support:** `CanExecutePlayerAction` and `PlayerActionLabel` properties for primary actions.
  - Handles unit, hex, and facing selection events.
  - **Action Execution:** `ExecutePlayerAction()` method for executing the primary player action.
  - Exposes available actions and the ability to execute the primary player action.
  - Provides a consistent contract for all concrete UI states.

### 2. Concrete UI States
- **DeploymentState:** Manages user interactions during unit deployment (selecting unit, hex, and facing).
- **MovementState:** Handles movement phase interactions, including unit selection, movement type selection, path selection, and move execution. Supports complex multi-step workflow with piloting skill roll probability display.
- **WeaponsAttackState:** Orchestrates weapon attack declaration, target selection, weapon configuration (including torso rotation), and attack confirmation.
- **EndState:** Handles end-of-turn or game interactions.
- **IdleState:** Represents a passive state when no action is required from the user.

### 3. Supporting Types
- **StateActions:** Represents available actions (buttons or menu items) in the current UI state.
- **PlayerAction:** Represents the primary action taken by the player in the current UI state.
- **MovementStep:** Enum representing substates within movement phase:
  - `SelectingUnit` - Choose unit to move
  - `SelectingMovementType` - Choose walk, run, jump, etc.
  - `SelectingTargetHex` - Choose destination hex
  - `SelectingDirection` - Choose final facing direction
  - `ConfirmMovement` - Confirm and execute movement
  - `Completed` - Movement finished
- **WeaponsAttackStep:** Enum representing substates within weapons attack phase:
  - `SelectingUnit` - Choose attacking unit
  - `ActionSelection` - Choose attack action or configuration
  - `WeaponsConfiguration` - Configure weapons (e.g., torso rotation)
  - `TargetSelection` - Select targets for weapons

---

## Enhanced State Functionality

### Movement State Features
- **Multi-Step Workflow:** Guides users through unit selection, movement type choice, target selection, and confirmation.
- **Movement Type Support:** Handles walking, running, jumping, standing still, and prone movement options.
- **Piloting Skill Integration:** Displays fall probabilities for risky movements using `PilotingSkillCalculator.GetPsrBreakdown()`.
- **Prone Mech Support:** 
  - Facing changes for prone mechs (1 MP per hexside while remaining in same hex)
  - Standup attempts with PSR requirements and phase restrictions

### Weapons Attack State Features
- **Torso Rotation:** Supports mech torso rotation for improved firing arcs.
- **Weapon Configuration:** Handles weapon setup and targeting assignments.
- **Range Highlighting:** Visual feedback for weapon ranges and valid targets.
- **Multi-Target Support:** Allows targeting multiple units with different weapons.

---

## Relationships & Data Flow

- Each UI state is instantiated and managed by the main ViewModel (e.g., `BattleMapViewModel`), which delegates user input to the current state.
- UI states interact with the game model, map, and units from MakaMek.Core to determine available actions and handle input.
- State transitions are triggered by user actions, game events, or completion of required steps.
- **Piloting Skill Integration:** States can access piloting skill calculations for displaying fall probabilities and PSR requirements.
- The design supports phase-specific validation, action availability, and feedback, decoupling UI logic from core game rules.

---

## Extensibility Points
- **New Phases:** Add new UI states by implementing `IUiState` for additional game phases or custom interactions.
- **Custom Actions:** Extend `StateAction` or add new substates for richer interaction flows.
- **UI/UX Enhancements:** UI states can be extended to support new input paradigms, tooltips, contextual help, or probability displays.

---

## Architecture Context

The UiStates module is part of the MakaMek.Presentation layer, which implements the presentation logic in the MVVM architecture:

```
+-------------------+
|  MakaMek.Avalonia |    Platform-specific UI implementation
+-------------------+
          ↑
          |
+-------------------+
| MakaMek.Presentation |  ViewModels, UiStates, and other presentation logic
+-------------------+
          ↑
          |
+-------------------+
|   MakaMek.Core    |    Core game logic and models
+-------------------+
```

This separation allows the presentation logic to be tested independently of the UI implementation, and enables potential future UI implementations on different platforms while reusing the same presentation logic.

---

## Summary

The `UiStates` module provides a flexible, maintainable framework for managing user interactions across all game phases. By encapsulating phase-specific logic and input handling, it enables clear separation of concerns and supports future UI and gameplay extensions. The enhanced functionality includes piloting skill integration, multi-step workflows, and advanced mech capabilities like torso rotation and prone movement.
