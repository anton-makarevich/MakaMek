# Map Module Architecture (MakaMek.Core)

## Overview
The Map module implements the core logic for the game's hex-based battle map. It encapsulates the structure, manipulation, and querying of map tiles (hexes), terrain, movement/pathfinding, line-of-sight (LOS), and firing arcs. The design supports extensibility (e.g., new terrain types, custom map generation) and efficient spatial calculations critical for gameplay.

---

## Main Components

### 1. BattleMap
- **Purpose:** Central manager for the game map, holding all hexes and providing high-level operations.
- **Responsibilities:**
  - Stores map dimensions and a dictionary of hexes (by coordinates).
  - Adds, retrieves, and iterates over hexes.
  - Pathfinding: Finds optimal paths between positions, considering movement cost, facing, and prohibited hexes. Supports both standard and jump movement.
  - Computes reachable hexes for a given unit, considering movement points, terrain, and prohibited areas.
  - Line-of-sight (LOS): Determines visibility between hexes, factoring in height, terrain, and intervening hexes. Uses a cache for performance.
  - Exposes utility methods for LOS hex listing, LOS cache clearing and map boundary validation.
  - Converts the map to data objects for serialization.

### 2. Hex
- **Purpose:** Models a single hex tile on the map.
- **Responsibilities:**
  - Stores position (`HexCoordinates`), level (height), and an optional theme.
  - Manages a set of terrain objects, supporting addition, removal, and queries.
  - Calculates movement cost (highest terrain cost), ceiling (level plus max terrain height), and terrain types present.
  - Converts itself to a serializable data object.

### 3. HexCoordinates
- **Purpose:** Represents a position in the hex grid using axial and cube coordinates.
- **Responsibilities:**
  - Stores Q (column), R (row), and derived S/X/Y/Z cube coordinates for advanced math.
  - Provides pixel positions for rendering.
  - Supplies methods for adjacency, neighbor lookup, direction calculation, and distance measurement.
  - Implements algorithms for lines between hexes (for LOS), firing arc calculations, and range queries.
  - Supports conversion to/from data objects and string representations.

### 4. HexDirection & HexPosition
- **HexDirection:** Enum representing the six possible directions in a hex grid (Top, TopRight, BottomRight, Bottom, BottomLeft, TopLeft).
- **HexPosition:** Combines a coordinate and facing direction, used for pathfinding and movement logic.

### 5. Pathfinding & Reachability
- **Pathfinding:**
  - The map supports both standard (terrain/facing-aware) and jump (distance-only) movement.
  - Standard pathfinding accounts for turning costs, movement costs per terrain, and optionally prohibited hexes.
  - Returns a sequence of `PathSegment` objects representing each step (move/turn) with associated cost.
  - **Enhanced Parameters:** Both `FindPath` and reachability methods now support `prohibitedHexes` parameter for avoiding specific areas.
- **Reachability:** Efficiently computes all hexes reachable within a movement budget, considering facing, terrain, and prohibited areas.
- **Jump movement:** Ignores terrain/facing, each hex costs 1. Supports prohibited hexes for tactical restrictions.

### 6. Line-of-Sight (LOS)
- **LOS Calculation:**
  - Determines if two hexes are mutually visible, considering height interpolation and terrain intervening factors.
  - Uses a line-drawing algorithm on the hex grid, with special handling for ambiguous (divided) segments.
  - LOS is blocked if intervening height or total terrain factor exceeds thresholds.
  - LOS paths are cached for efficiency.
- **LOS Hex Listing:** 
  - `GetHexesAlongLineOfSight` method returns detailed hex information along the LOS path.
  - Returns all hexes along the LOS between two points for tactical analysis.
- **Boundary Validation:** `IsOnMap` method provides efficient boundary checking for coordinate validation.

### 7. Firing Arcs
- **FiringArc:** Enum representing forward, left, right, and rear arcs.
- **Arc Calculation:**
  - HexCoordinates provides methods to determine if a target is within a given arc (using vector math and angles).
  - Used for attack/defense logic and UI highlighting.

### 8. Terrain System
- **Terrains:**
  - Each hex may have multiple terrain objects (e.g., woods, clear), each with movement cost, height, and intervening (LOS) factor.
  - Terrain types are extensible via the `Terrains` subdirectory.
  - Terrain affects both movement and LOS calculations.

### 9. Factories
- **BattleMapFactory/IBattleMapFactory:**
  - Abstract map creation logic, enabling custom map generation for different scenarios or tests.

---

## Relationships & Data Flow
- `BattleMap` manages all `Hex` instances and exposes high-level operations.
- Each `Hex` references its `HexCoordinates` and a set of `Terrain` objects.
- Pathfinding, LOS, and firing arc logic operate on coordinates and hex data.
- Factories encapsulate map construction logic, supporting extensibility.

---

## Extensibility Points
- **Terrains:** Add new terrain types by extending the base `Terrain` class.
- **Map Generation:** Implement custom factories for scenario-specific maps.
- **LOS/Pathfinding:** Algorithms are modular and can be replaced or extended for advanced rules.

---

## Summary
The Map module offers a robust, extensible foundation for hex-based spatial logic in MakaMek. It supports complex movement, terrain, and visibility rules while remaining performant and maintainable. All core responsibilities (map structure, pathfinding, LOS, terrain, and arcs) are encapsulated and decoupled for easy evolution.
