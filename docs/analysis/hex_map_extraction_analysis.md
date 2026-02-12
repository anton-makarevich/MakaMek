# Hex Map Extraction Analysis

This document analyzes the strategies for extracting common hex map functionality from `MakaMek.Core` into a shared library (`Sanet.HexMap` or similar). The goal is to maximize code reuse for other hex-based games while preserving BattleTech-specific rules in the core project.

## Current State Analysis

The `src/MakaMek.Core/Models/Map` directory currently mixes generic hex grid mathematics with specific BattleTech game rules.

### Generic Components (Move to Shared Library)
These components are mathematically generic and reusable for any flat-top hex grid system.

| Component | Responsibility | Dependencies to Abstract |
|-----------|----------------|--------------------------|
| `HexCoordinates` | Axial/Cube coordinate system, distance, neighbors. | None. Pure logic. |
| `HexDirection` | Enum for 6 directions. | None. |
| `HexPosition` | Tuple of `HexCoordinates` + `HexDirection`. | None. |
| `PathSegment` | Edge between two nodes with cost. | None. |
| `HexCoordinateData` | DTO for coordinates. | None. |

### Semi-Generic Components (Refactor & Move)
These components contain structural logic that is generic but currently coupled to BattleTech rules.

| Component | Issue | Proposed Solution |
|-----------|-------|-------------------|
| `MovementPath` | Contains `IsReversed` (mech backing up) and `HexesTraveled` (BattleTech specific calculation). | Make `MovementPath` a generic container. Subclass `BattleTechMovementPath` or use Metadata dictionary. Alternatively, keep `MovementPath` simple and move specific calculations to a `MovementAnalyzer` service. |
| `IBattleMap` / `BattleMap` | Mixes map storage with specific Pathfinding and LOS rules. | Split into `IHexMap<THex>` (storage) and separate `Pathfinder` and `LosCalculator` services. |
| `LineOfSightCache` | Caches LOS results. | Generic concept, but implementation details (what is cached) might vary. Can be made generic with `TKey` -> `TValue`. |

### BattleTech Specific Components (Stay in MakaMek.Core)
These are deeply tied to the specific game rules.

| Component | Reason |
|-----------|--------|
| `Hex` | Properties like `Level`, `MovementCost`, `Theme`, `Terrains` collection. |
| `Terrain` classes | `MakaMekTerrains` enum, specific terrain types (Water, Woods). |
| `BattleMap` logic | `FindPath` uses specific movement costs (turning logic + terrain). `HasLineOfSight` uses specific height interpolation formula and intervening tokens. |

## Proposed Architecture

### 1. New Project: `Sanet.HexMap` (Shared Library)

This library will contain the core mathematical primitives, generic map structure, and base terrain concepts.

#### Core Primitives
- `HexCoordinates`, `HexDirection`, `HexPosition`
- `IHex` interface (with `Coordinates` property).
- `HexMap<THex>`: Generic container for hexes.

#### Terrain Concept
- `Terrain` (Base class or Interface):
    - Basic properties like `Id`, `Name`, `MovementCostPenalty`, `ObscurationLevel`.
    - `Hex` in the shared library will hold a collection of `Terrain` objects.

#### Generic Algorithms (Extensions)
Instead of service interfaces, algorithms will be exposed as extension methods on `IHexMap` or via static helpers.
- `PathfindingExtensions`:
    - `FindPath<THex>(this IHexMap<THex> map, ...)`: Generic A* implementation.
- `LineOfSightExtensions`:
    - `GetLineOfSight<THex>(this IHexMap<THex> map, ...)`: Generic line drawing (Bresenham/Supercover).

### 2. `MakaMek.Core` Implementation

MakaMek.Core consumes the new library and extends it with specific rules using the Extension pattern.

#### Extended Data Structures
- `MakaMekTerrain : Terrain`: Adds BattleTech specifics (e.g., `IsFlammable`, `Height`, `PSRModifier`).
- `Hex : IHex` (or `Hex : HexBase`) -> Contains `List<MakaMekTerrain>`.
- `BattleMap : HexMap<Hex>` (or wraps it).

#### Logic Implementation (Extensions)
BattleTech-specific logic will be implemented as extension methods on `IBattleMap` (or `IHexMap<Hex>`), similar to the existing `BattleMapExtensions.cs`.

- **Movement Extensions** (`BattleMapMovementExtensions.cs`):
    - `FindBattleTechPath(...)`: Calls the generic `FindPath` but provides a specific `CostFunction` that accounts for:
        - `MakaMekTerrain` costs.
        - Facing changes (turning costs).
        - Movement Modes (Walk/Run/Jump).
        - Heat/Critical damage effects.
    
- **Line of Sight Extensions** (`BattleMapLosExtensions.cs`):
    - `HasBattleTechLineOfSight(...)`:
        - Calls generic `GetLineOfSight` to get candidate hexes.
        - Applies BT-specific `InterpolateHeight` logic.
        - Checks `MakaMekTerrain` properties for blocking (Woods, etc.).


## Extraction Plan

### Phase 1: Core Primitives extraction
1.  Create `Sanet.HexMap` project.
2.  Move `HexCoordinates`, `HexDirection`, `HexPosition`, `HexCoordinateData`.
3.  Update namespaces in `MakaMek.Core`.

### Phase 2: Map Infrastructure
1.  Create `IHexMap<T>` and `HexMapBase<T>` in shared library.
2.  Refactor `BattleMap` to inherit `HexMapBase<Hex>`.
3.  Extract generic `AddHex`, `GetHex`, `IsOnMap` methods.

### Phase 3: Pathfinding & LOS Support
1.  Extract `HexCoordinates.LineTo` logic into a static utility or extensions in shared library (Geometry helper).
2.  Create a generic `Pathfinding` service in shared library.
    *   *Note on Pathfinding*: BattleTech pathfinding is complex because nodes are `(Hex, Facing)`, not just `Hex`. The standardized A* must support `Node = (Coord, Facing)` state space.

## Complexity & Risks

1.  **State Space**: Generic A* usually works on Nodes. In BattleTech, a Node is `(Coordinates, Facing)`. In Civ, it's just `Coordinates`. The shared library should support arbitrary state nodes or generic "Graph" pathfinding.
2.  **Performance**: `HexCoordinates` struct optimizations (like `DistanceTo`) are critical. Ensure moving them doesn't regress performance (should be fine, it's pure math).
3.  **Refactoring Scope**: Changing `HexCoordinates` namespace will touch *hundreds* of files.

## Recommendation

Start with **Phase 1** (Coordinates & Primitives) immediately. This decouples the fundamental math from the game logic. Follow with Phase 2 (Map storage) once the primitives are stable. Phase 3 (Pathfinding) requires careful design to align with the directional nature of BattleTech movement.
