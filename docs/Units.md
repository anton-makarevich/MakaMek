# Units Module Architecture (MakaMek.Core)

## Overview
The Units module provides the core abstractions and logic for all controllable units in the game (e.g., mechs, vehicles). It models unit composition, part/armor structure, movement, heat, components (weapons, engines, etc.), piloting/crew systems, damage tracking, and extensible subsystems. The module is designed for flexibility, supporting new unit types, components, and gameplay rules.

---

## Main Components

### 1. Unit
- **Purpose:** Abstract base class for all game units (e.g., mechs, vehicles).
- **Responsibilities:**
  - Stores chassis, model, tonnage, owner, status, and unique ID.
  - **Status Properties:** Boolean properties (`IsDestroyed`, `IsProne`, `IsImmobile`) for cleaner status checking.
  - Maintains a list of `UnitPart` objects representing the physical structure (e.g., torso, arms, legs).
  - **Phase-Based Damage Tracking:** `TotalPhaseDamage` property tracks damage accumulated during each game phase.
  - **Phase State Management:** `ResetPhaseState()` method called during phase transitions to reset damage tracking.
  - Calculates weight class, movement points (by type), and manages deployment (location/facing on map).
  - **Enhanced Pilot/Crew System:** `IPilot` interface with `MechWarrior` implementation supporting gunnery/piloting skills.
  - **Movement Capabilities:** `CanJump` property for jump movement validation based on available jump jets.
  - Tracks and manages heat generation/dissipation (from movement, weapons, and components).
  - Handles turn state: movement, attacks, heat application, and reset logic.
  - Manages weapon attack declarations and target assignment.
  - Provides aggregate armor/structure calculations.
  - Abstracts battle value calculation and heat effect logic for subclasses.

### 2. UnitPart
- **Purpose:** Models a structural part of a unit (e.g., arm, torso, leg).
- **Responsibilities:**
  - Stores name, location, armor/structure values, and slot capacity.
  - Manages a collection of installed `Component` objects (e.g., weapons, heat sinks, jump jets).
  - Handles component mounting/unmounting, slot management, and destruction state.
  - Applies and distributes damage (armor first, then structure) based on hit direction.
  - Supports queries for installed components by type or slot.

### 3. Components System
- **Component:** Base class for all installable items (weapons, engines, heat sinks, jump jets, etc.).
- **Responsibilities:**
  - Encapsulates size, mounting logic, and type-specific properties (e.g., heat, damage for weapons).
  - **Component Status:** `ComponentStatus` enum tracks component state (Active, Destroyed, Removed, Lost, Deactivated, Damaged).
  - Supports extensibility via subdirectories: `Weapons`, `Engines`, `Internal`, etc.
  - Components can be fixed or movable, and may occupy multiple slots.

### 4. Pilot/Crew System
- **IPilot:** Interface for unit operators with skill ratings and status tracking.
- **MechWarrior:** Concrete implementation for mech pilots with:
  - Gunnery and Piloting skill ratings (lower is better)
  - Injury tracking and unconsciousness states
  - Integration with piloting skill roll calculations

### 5. Enhanced Movement System
- **Prone Mech Capabilities:**
  - **Facing Changes:** Prone mechs can change facing at start of Movement Phase for 1 MP per hexside while remaining in same hex
  - **Standup Attempts:** Prone mechs can attempt to stand up, which may require piloting skill rolls and affects other actions in the same phase
- **Jump Movement:** `CanJump` property validates jump capability based on functional jump jet components
- **Movement Validation:** Enhanced validation for movement types considering unit status and component availability

### 6. Submodules
- **Mechs/:** Contains concrete unit subclasses (e.g., specific mech types) inheriting from `Unit`.
- **Pilots/:** Models pilots/crew that may be assigned to units, affecting stats and abilities.
- **Components/:** Houses all component types and their logic.

### 7. Enums & Value Types
- **ArmorLocation, PartLocation:** Enumerate possible part/armor locations for hit/damage logic.
- **HitDirection:** Indicates direction of incoming damage (front, side, rear).
- **MovementType:** Enumerates movement modes (walk, run, jump, sprint, MASC, prone, etc.).
- **UnitStatus:** **Enhanced Flags Enum** tracks operational state with validation (Active, Shutdown, Prone, Immobile, Destroyed).
- **ComponentStatus:** Tracks component operational state (Active, Destroyed, Removed, Lost, Deactivated, Damaged).
- **WeightClass:** Categorizes units by tonnage (light, medium, heavy, assault).
- **PilotingSkillRollType:** Enum for PSR system integration defining various conditions requiring piloting skill rolls.
- **IManufacturedItem:** Interface for items that can be produced or referenced generically.

---

## Relationships & Data Flow
- `Unit` aggregates multiple `UnitPart` objects, each of which manages its own components and state.
- `UnitPart` references its parent `Unit` and exposes methods for damage, component management, and status.
- Components are installed into parts, affecting unit stats (movement, heat, firepower, etc.).
- Movement, heat, and attack logic flow from the `Unit` level down to parts and components.
- **Pilot Integration:** Pilots affect unit performance through skill ratings and injury states.
- **Phase Integration:** Units participate in phase-based damage tracking and state management.
- Submodules (Mechs, Pilots) extend or compose units for specific gameplay roles.

---

## Extensibility Points
- **New Units:** Create new subclasses of `Unit` (e.g., for different mech types or vehicles).
- **New Parts:** Extend `UnitPart` for custom part logic or new structural features.
- **New Components:** Add new component types in the Components subdirectory.
- **Pilots/Crew:** Extend pilot logic for advanced crew abilities and effects.
- **Status Management:** Add new status flags or validation logic for custom unit states.
- **Movement Types:** Extend movement system for new movement modes or restrictions.
- **Enums/Rules:** Add or modify enums and rules to support new gameplay mechanics.

---

## Summary
The Units module provides a flexible, extensible framework for modeling all game units, their structure, equipment, operational state, and crew. Its design supports complex composition, modular upgrades, detailed simulation of combat and movement, piloting skill integration, and phase-based state management, while remaining maintainable and open to future expansion.
