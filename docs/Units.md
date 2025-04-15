# Units Module Architecture (MakaMek.Core)

## Overview
The Units module provides the core abstractions and logic for all controllable units in the game (e.g., mechs, vehicles). It models unit composition, part/armor structure, movement, heat, components (weapons, engines, etc.), and extensible subsystems. The module is designed for flexibility, supporting new unit types, components, and gameplay rules.

---

## Main Components

### 1. Unit
- **Purpose:** Abstract base class for all game units (e.g., mechs, vehicles).
- **Responsibilities:**
  - Stores chassis, model, tonnage, owner, status, and unique ID.
  - Maintains a list of `UnitPart` objects representing the physical structure (e.g., torso, arms, legs).
  - Calculates weight class, movement points (by type), and manages deployment (location/facing on map).
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
  - Supports extensibility via subdirectories: `Weapons`, `Engines`, `Internal`, etc.
  - Components can be fixed or movable, and may occupy multiple slots.

### 4. Submodules
- **Mechs/:** Contains concrete unit subclasses (e.g., specific mech types) inheriting from `Unit`.
- **Pilots/:** Models pilots/crew that may be assigned to units, affecting stats and abilities.
- **Components/:** Houses all component types and their logic.

### 5. Enums & Value Types
- **ArmorLocation, PartLocation:** Enumerate possible part/armor locations for hit/damage logic.
- **HitDirection:** Indicates direction of incoming damage (front, side, rear).
- **MovementType:** Enumerates movement modes (walk, run, jump, sprint, masc, etc.).
- **UnitStatus:** Tracks operational state (active, shutdown, destroyed, etc.).
- **WeightClass:** Categorizes units by tonnage (light, medium, heavy, assault).
- **IManufacturedItem:** Interface for items that can be produced or referenced generically.

---

## Relationships & Data Flow
- `Unit` aggregates multiple `UnitPart` objects, each of which manages its own components and state.
- `UnitPart` references its parent `Unit` and exposes methods for damage, component management, and status.
- Components are installed into parts, affecting unit stats (movement, heat, firepower, etc.).
- Movement, heat, and attack logic flow from the `Unit` level down to parts and components.
- Submodules (Mechs, Pilots) extend or compose units for specific gameplay roles.

---

## Extensibility Points
- **New Units:** Create new subclasses of `Unit` (e.g., for different mech types or vehicles).
- **New Parts:** Extend `UnitPart` for custom part logic or new structural features.
- **New Components:** Add new component types in the Components subdirectory.
- **Pilots/Crew:** Extend pilot logic for advanced crew abilities and effects.
- **Enums/Rules:** Add or modify enums and rules to support new gameplay mechanics.

---

## Summary
The Units module provides a flexible, extensible framework for modeling all game units, their structure, equipment, and operational state. Its design supports complex composition, modular upgrades, and detailed simulation of combat and movement, while remaining maintainable and open to future expansion.
