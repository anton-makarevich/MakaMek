# Components System Architecture (MakaMek.Core)

## Overview
The Components system provides a flexible and extensible framework for modeling all equipment and internal systems that can be installed in units. This includes weapons, engines, actuators, internal components, ammunition, and specialized equipment. The system supports multi-location mounting, component-specific state management, and dynamic component creation through a provider pattern.

---

## Core Architecture

### 1. Component Class Hierarchy

#### Base Component Class
The `Component` class serves as the abstract base for all installable items.

#### Component Categories
Components are organized into logical categories:

- **Actuators**: Movement-related components (`ShoulderActuator`, `UpperArmActuator`, etc.)
- **Internal**: Critical systems (`Gyro`, `LifeSupport`, `Cockpit`, `Sensors`)
- **Engines**: Power systems with variable configurations (`Engine` with different types)
- **Weapons**: Combat systems with ammunition dependencies
- **Equipment**: General equipment (`HeatSink`, `JumpJets`, etc.)

### 2. Component Definition System

#### ComponentDefinition Records
Immutable definitions separate static properties from runtime state.

#### Specialized Definitions
- **ActuatorDefinition**: Single-slot, non-removable actuators
- **InternalDefinition**: Critical systems with variable health points
- **EquipmentDefinition**: General equipment with configurable properties
- **EngineDefinition**: Variable-size engines based on type
- **WeaponDefinition**: Combat systems with damage and range properties
- **AmmoDefinition**: Linked to specific weapon types

### 3. Component Data and State Management

#### ComponentData Structure
Persistent state representation for serialization and restoration.

#### Component-Specific State Data
Polymorphic state data for specialized components:

**Examples:**
- `EngineStateData`: Engine type and rating
- `AmmoStateData`: Ammunition count and type

---

## Multi-Location Component System

### Slot Assignment Model
Components can span multiple locations through `CriticalSlotAssignment`.

### Mounting Process
1. **Slot Validation**: Check availability and contiguity requirements
2. **Assignment Creation**: Create slot assignments for each location
3. **Component Registration**: Add component to affected unit parts
4. **State Synchronization**: Update component mounting status

### Multi-Location Examples
- **XL Engine**: 6 slots in Center Torso + 2 slots each in side torsos

---

## Component Status Management

### ComponentStatus Enum
Comprehensive status tracking.

### Status Calculation Logic
Status is dynamically calculated based on (with semantics):
- Destroyed: Component’s own HP reduced to zero (in any location)
- Lost: Location destroyed causing loss of assigned slots for the component
- Removed: Explicitly unmounted/detached during refit
- Activation state (manual control)

---

## Component Provider Pattern

### IComponentProvider Interface
Centralized component creation and definition management.

### ClassicBattletechComponentProvider
Default implementation providing:
- **Definition Registry**: Static definitions for all component types
- **Factory Methods**: Component instantiation with state restoration

## Integration with Unit Parts

### UnitPart Component Management
Unit parts manage component mounting and slot allocation:

### Slot Management
- **Automatic Assignment**: Find available consecutive slots
- **Manual Assignment**: Use specified slots with validation
- **Conflict Detection**: Prevent overlapping assignments
- **Destruction Handling**: Component loss when location destroyed

---

## Specialized Component Types

### Engine Components
Variable-size components with type-specific behavior:
- **Fusion Engine**: 6 slots in Center Torso
- **XL Fusion**: 6 CT + 2 slots each in side torsos
- **Light Engine**: 4 slots in Center Torso
- **Heat Integration**: Built-in heat sink capacity

### Weapon Systems
Combat components with ammunition dependencies:
- **Damage Calculation**: Range-based damage modification
- **Heat Generation**: Firing heat accumulation
- **Ammunition Tracking**: Linked ammo components
- **Explosion Risk**: Ammunition explosion mechanics

### Internal Components
Critical systems with special destruction effects:
- **Cockpit**: Pilot death on destruction
- **Gyro**: Movement penalties when damaged
- **Life Support**: Heat-based pilot damage
- **Sensors**: Targeting penalties

### Actuators
Movement-enabling components:
- **Arm Actuators**: Weapon accuracy modifiers
- **Leg Actuators**: Movement capability
- **Default Mounting**: Predefined slot assignments

---

## Component Creation and Loading

### MechFactory Integration
Component instantiation during unit creation.

### Data Provider Integration
External data loading through `MtfDataProvider`:
- **Component Mapping**: String names to `MakaMekComponent` enum
- **Size Calculation**: Dynamic sizing based on component type
- **State Data Creation**: Component-specific data generation

---

## Extensibility Points

### Adding New Component Types
1. **Enum Extension**: Add to `MakaMekComponent` enum
2. **Definition Creation**: Create appropriate `ComponentDefinition`
3. **Component Class**: Implement component-specific behavior
4. **Provider Registration**: Add to factory and definition dictionaries
5. **State Data**: Create `ComponentSpecificData` if needed

### Custom Component Behaviors
- **Override Methods**: `Hit()`, `Activate()`, `Deactivate()`
- **Special Properties**: `CanExplode`, `GetExplosionDamage()`
- **State Persistence**: `GetSpecificData()`

### Integration Points
- **Damage System**: Component hit resolution
- **Heat System**: Heat generation and dissipation
- **Movement System**: Actuator and engine integration
- **Combat System**: Weapon and ammunition mechanics

---

## Summary

The MakaMek Components system provides a robust, extensible framework for modeling all equipment and systems within game units:

- **Flexible Architecture**: Support for diverse component types and behaviors
- **Multi-Location Support**: Components can span multiple unit locations
- **State Management**: Comprehensive status tracking and persistence
- **Provider Pattern**: Centralized creation and definition management
- **Integration**: Seamless interaction with unit parts and game systems

The system is designed for extensibility, allowing easy addition of new component types while maintaining consistency and performance across the entire component ecosystem.