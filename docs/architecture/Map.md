# Map Module Architecture (MakaMek.Map)

## Overview
The Map module implements the core logic for the game's hex-based battle map. It encapsulates the structure, manipulation, and querying of map tiles (hexes), terrain, movement/pathfinding, line-of-sight (LOS), and firing arcs. The design supports extensibility (e.g., new terrain types, custom map generation) and efficient spatial calculations critical for gameplay.

`BattleMap` is a thin hex-store façade with an unchanged public API. Movement and visibility algorithms live in dedicated collaborators under `Pathfinding/`; map serialization lives in `Data/`. The split keeps the public surface stable while allowing each algorithm to be understood, tested, and changed on its own.

---

## Main Components

### 1. BattleMap
- **Purpose:** Public façade for the game map, holding all hexes and delegating traversal work.
- **Responsibilities:**
  - Stores map dimensions and a dictionary of hexes (by coordinates) in `HexGrid`.
  - Adds, retrieves, and iterates over hexes; validates map boundaries.
  - Delegates pathfinding to `HexPathfinder`, reachability to `HexReachabilityCalculator` and LOS to `LineOfSightCalculator` (see sections 5 and 6).
  - Handles jump movement (`FindJumpPath` and helpers) and converts resolved paths to `PathSegment`s.
  - Clears the LOS cache and converts the map to data objects for serialization.

### 1.1 HexGrid / IHexGrid
- **Purpose:** Owns hex storage; exposes the narrow read model the traversal algorithms consume.
- **Responsibilities:**
  - `HexGrid` (internal) stores hexes by `HexCoordinates`, enforces 1-based map bounds, and stamps the biome onto added hexes.
  - `IHexGrid` (internal) exposes only `Width`, `Height`, `GetHex` and `IsOnMap`, so algorithms never depend on the full `BattleMap` façade.

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
- **Collaborators:**
  - `HexPathfinder` — A* over the hex graph for ground movement, with `PathFindingMode.Shortest` and `PathFindingMode.Longest` strategies, caching and turning-segment construction. It knows no terrain rules.
  - `IHexMovementRules` / `HexMovementRules` — supplies the legal surface transitions into adjacent hexes and their entry/elevation cost (`HexTransition`), including bridge clearance, road/pavement access and elevation limits.
  - `PathRequest` — immutable inputs for one ground-movement search; `HexTransition` — one legal destination surface with its cost.
  - `HexReachabilityCalculator` — walk and jump reachability, both built on `HexBreadthFirstSearch` (cheapest cost per discovered node, re-visiting only when a cheaper route arrives).
- **Pathfinding:**
  - The map supports both standard (terrain/facing-aware) and jump (distance-only) movement.
  - Standard pathfinding accounts for turning costs, movement costs per terrain, and optionally prohibited hexes.
  - Returns a sequence of `PathSegment` objects representing each step (move/turn) with associated cost.
  - **Enhanced Parameters:** Both `FindPath` and reachability methods now support `prohibitedHexes` parameter for avoiding specific areas.
- **Jump profile:** Jump paths carry a single vertical profile on `HexPosition.AirborneLevel`. The destination is grounded (`null`); the start is grounded except for an adjacent jump, whose start carries the approach waypoint. The last translation's `From` (the approach position, `destination - 1`) is a hard waypoint at `max(referenceLevel(approach), referenceLevel(destination)) + offset`, where the offset is supplied by the rules layer (`IRulesProvider.GetDfaAirborneBaseOffset()`, currently 2 for the DFA mid-air base); the map itself defaults to 0. Line of sight adds the unit height on top. An adjacent jump therefore stores it on the start position. Other intermediate levels are linearly interpolated, rounded up, and clamped between the hex reference level and the jump ceiling. The same values feed path validity and DFA LOS; presentation is flat top-down 2D and does not render a vertical arc. A completed DFA leaves that approach waypoint on the attacker's `Position` (airborne for LOS) until the attack resolves.
- **Reachability:** Efficiently computes all hexes reachable within a movement budget, considering facing, terrain, and prohibited areas.
- **Jump movement:** Ignores terrain/facing for cost; each crossed hex costs 1 MP. The path must be a shortest route and every crossed hex (destination included) must satisfy `referenceLevel(hex) <= startLevel + availableJumpingMP`, where `startLevel` is the unit's standing level in the start hex (bridge deck when on a bridge, riverbed when in water). Jump pathfinding picks the first legal shortest route deterministically rather than enumerating every route. `GetJumpReachableHexes` runs one breadth-first search through clear hexes and includes a destination only when its legal search distance equals the geometric distance. Prohibited hexes only exclude destinations: a jump passes over them. The jump cache key includes the jump MP budget because the ceiling depends on it.

### 6. Line-of-Sight (LOS)
- **LOS Calculation (`LineOfSightCalculator`):**
  - Determines if two hexes are mutually visible, considering height interpolation and terrain intervening factors.
  - Walks the segments produced by `HexCoordinates.LineTo` and evaluates each intervening hex; it never re-implements line interpolation.
  - Uses a line-drawing algorithm on the hex grid, with special handling for ambiguous (divided) segments.
  - LOS is blocked if intervening height or total terrain factor exceeds thresholds.
  - Optional attacker and target base levels let an airborne position (a pending DFA approach) supply its absolute endpoint level instead of the surface standing level; such an endpoint is never treated as submerged. The path cache still keys on coordinates only.
  - LOS paths are cached per coordinate pair in `LineOfSightCache`.
- **LOS Hex Listing:** every `LineOfSightResult` carries `HexPath`, the resolved intervening hexes with their individual intervening factors.
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

### 9. Factories & Generators
- **BattleMapFactory/IBattleMapFactory:**
  - Abstract map creation logic, enabling custom map generation for different scenarios or tests.
- **PathWalker (`Generators/`):**
  - Shared traversal primitive for the terrain path generators: a 1-based bounds check plus a breadth-first `Flood` that grows a set of hexes from a start hex.
  - Each generator (`PatchGenerator`, `RiverPathGenerator`, `RoadPathGenerator`) supplies its own per-hex legality predicate; the walk shape lives here once.

---

## Relationships & Data Flow
- `BattleMap` owns a `HexGrid` and delegates pathfinding, reachability and LOS to `HexPathfinder`, `HexReachabilityCalculator` and `LineOfSightCalculator`.
- The traversal collaborators depend on `IHexGrid` (storage) and, for movement, `IHexMovementRules` (terrain and elevation rules); both are injectable for tests.
- Each `Hex` references its `HexCoordinates` and a set of `Terrain` objects.
- Pathfinding, LOS, and firing arc logic operate on coordinates and hex data.
- Factories encapsulate map construction logic, supporting extensibility.

---

## Extensibility Points
- **Terrains:** Add new terrain types by extending the base `Terrain` class.
- **Map Generation:** Implement custom factories for scenario-specific maps.
- **LOS/Pathfinding:** The algorithms are isolated behind `LineOfSightCalculator`, `HexPathfinder`, `HexReachabilityCalculator` and `IHexMovementRules`, so they can be replaced or extended for advanced rules without touching `BattleMap`.

---

## Summary
The Map module offers a robust, extensible foundation for hex-based spatial logic in MakaMek. It supports complex movement, terrain, and visibility rules while remaining performant and maintainable. All core responsibilities (map structure, pathfinding, LOS, terrain, and arcs) are encapsulated and decoupled for easy evolution.
