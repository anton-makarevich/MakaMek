# Roads, Pavement & Bridges — Product Requirements Document

**Date:** 2026-05-30  
**Status:** Ready for Implementation

---

## Executive Summary

This document specifies the requirements for implementing road, pavement, and bridge terrain mechanics in MakaMek. Roads and paved areas allow units to ignore underlying terrain movement penalties while introducing a skidding hazard at Running speed. Bridges extend road mechanics over lower terrain or water and introduce weight-based collapse. The implementation extends the existing terrain, movement-phase, PSR, and to-hit modifier subsystems without altering their fundamental contracts.

---

## Background — BattleTech Rules Reference

The rules below are the authoritative source for this feature. Implementation must satisfy all of them except those explicitly marked **Out of Scope**.

### Pavement and Roads

- **Pavement** — large paved surfaces (landing fields, urban blocks, sidewalks).
- **Road** — narrow paved strip passing through other terrain types.
- **LOS & height** — roads and pavement sit at the printed level of the hex; they do not add height or block LOS.
- **Movement cost** — entering a paved/road hex costs **+0 MP** (total 1 MP when no elevation change).
- **"On road" status** — a unit is "on" a road only when it moves **from** a road/paved hex **to** another road/paved hex.
- **Underlying terrain ignored** — while on the road, the unit ignores the movement penalties and restrictions of the underlying terrain (e.g., heavy woods under a road still costs 1 MP).
- **Prohibited terrain** — a unit may traverse normally-prohibited terrain (e.g., deep water via bridge) while staying on the road.
- **Elevation changes** — units still pay normal MP for level changes even when following a road.
- **Backward movement** — units moving backward cannot change levels; this applies on roads and bridges.

### Skidding

- **Trigger** — a 'Mech using **Running** movement makes a **facing change** in a paved or road hex and then enters a new hex.
- **Piloting Skill Roll (PSR)** — the pilot must pass a PSR or the 'Mech skids.
- **Skid distance** — if the PSR fails, the 'Mech falls and slides a number of hexes equal to **⌈hexes moved ÷ 2⌉** (rounded up).
- **Skid damage** — the skidding 'Mech takes **half normal falling damage** per hex traveled during the skid.
- **To-hit vs. skidding unit** — **+2 to-hit modifier** against the skidding 'Mech.
- **Skidding unit's own attacks** — **+1 to-hit penalty** on attacks made by the skidding unit.

### Bridges

- **Definition** — a road is treated as a bridge when it passes over water or terrain at a lower elevation.
- **On top of bridge** — a unit moving road-to-road is on the bridge surface. It ignores the underlying terrain and follows all road rules.
- **Construction Factor (CF)** — each bridge hex has a CF representing the maximum tonnage it can support. When the total tonnage of units on a bridge hex exceeds its current CF, the bridge hex **collapses**; units fall to the underlying terrain and take standard falling damage.
- **Below bridge** *(Out of Scope v1)* — a unit not traveling on the road is on the underlying terrain, positioned underneath the bridge. This requires a z-layer positioning system not yet available.
- **Insufficient clearance** *(Out of Scope v1)* — if a unit cannot fit under a bridge and is not on top of it, the bridge acts as a building hex.

---

## Scope

### In Scope

| # | Capability |
|---|-----------|
| 1 | Road and Pavement terrain types with movement cost 1 (no underlying terrain penalty when on-road) |
| 2 | Bridge terrain type with configurable Construction Factor (CF) |
| 3 | "On road" status detection (source and destination both road/paved) |
| 4 | Skid PSR trigger on Running + facing change on paved/road hex |
| 5 | Skid outcome: fall + slide, damage per skid hex, attacker/target to-hit modifiers |
| 6 | Bridge CF check on unit entry; collapse → units fall to underlying terrain |
| 7 | Serialization via existing `TerrainData` record (no schema change) |
| 8 | Network propagation via existing `SegmentEvent` mechanism |

### Out of Scope (v1)

- Ground vehicle +1 MP pavement bonus (no vehicle unit type yet)
- VTOL/WiGE road-following through woods below treetop elevation
- Aerospace unit landing modifier on paved hexes
- Under-bridge unit positioning (requires z-layer system)
- Bridge acting as building hex for units with insufficient clearance

---

## Terrain Model

### New Enum Values

**File:** `src/MakaMek.Map/Models/Terrains/MakaMekTerrains.cs`

```csharp
Road,       // Narrow paved strip through other terrain
Pavement,   // Broad paved surface
Bridge,     // Paved road over lower terrain or water; Height encodes CF
```

### New Terrain Classes

All three live alongside the existing terrain classes in `src/MakaMek.Map/Models/Terrains/`.

#### `RoadTerrain`

| Property | Value |
|----------|-------|
| `Id` | `MakaMekTerrains.Road` |
| `Height` | `0` |
| `InterveningFactor` | `0` |
| `MovementCost` | `1` |

#### `PavementTerrain`

| Property | Value |
|----------|-------|
| `Id` | `MakaMekTerrains.Pavement` |
| `Height` | `0` |
| `InterveningFactor` | `0` |
| `MovementCost` | `1` |

#### `BridgeTerrain`

| Property | Notes |
|----------|-------|
| `Id` | `MakaMekTerrains.Bridge` |
| `Height` | Stores CF as a positive integer (e.g., `Height = 60` → CF 60 tons) |
| `InterveningFactor` | `0` |
| `MovementCost` | `1` |
| `ConstructionFactor` | Derived from `Height`; represents maximum supported tonnage |

`BridgeTerrain` is serialized via `TerrainData` with `Type = Bridge` and `Height = CF`. No schema change is required.

### Factory Extension

**File:** `src/MakaMek.Map/Models/Terrains/Terrain.cs` — `FromData()` and `GetTerrainType()` factory methods get new `case` entries for `Road`, `Pavement`, and `Bridge`.

---

## Movement Rules

### "On Road" Detection

A new extension method on `Hex` (file: `src/MakaMek.Map/Models/HexExtensions.cs`):

```csharp
public bool IsRoadOrPaved(Hex hex)
    => hex.HasTerrain(MakaMekTerrains.Road)
    || hex.HasTerrain(MakaMekTerrains.Pavement)
    || hex.HasTerrain(MakaMekTerrains.Bridge);
```

A unit is **on the road** for a given movement step when **both** the source hex and the destination hex satisfy `IsRoadOrPaved()`.

### Movement Cost Substitution

**File:** `src/MakaMek.Map/Models/BattleMap.cs` — segment cost calculation

When the unit is on the road (both hexes paved), the hex entry cost is **1 MP** (road surface cost) regardless of the underlying terrain's `MovementCost`. Elevation change costs are still added on top.

```
segmentCost = (isOnRoad ? 1 : destinationHex.MovementCost) + Math.Abs(elevationChange)
```

This change is localized to the segment cost calculation in `ConvertPathToSegments()` (or equivalent method). The `Hex.MovementCost` property itself is not changed.

### Backward Movement Restriction

The existing rule that backward movement cannot change levels remains unchanged and applies on roads and bridges. No new code is required.

---

## Skid Mechanics

### PSR Type

**File:** `src/MakaMek.Core/Data/Game/Mechanics/PilotingSkillRollType.cs`

```csharp
SkidCheck,   // Running + facing change on paved/road hex
```

### Segment Event Type

**File:** `src/MakaMek.Map/Models/SegmentEventType.cs`

```csharp
Skid,           // Mech skids after failed SkidCheck PSR
BridgeCollapse, // Bridge hex collapses under weight
```

### Skid PSR Context

**File:** `src/MakaMek.Core/Data/Game/Mechanics/PilotingSkillRollContexts/SkidCheckRollContext.cs`

```csharp
public record SkidCheckRollContext(int HexesMoved)
    : PilotingSkillRollContext(PilotingSkillRollType.SkidCheck);
```

`HexesMoved` is used to compute the skid distance (`⌈HexesMoved ÷ 2⌉`) if the roll fails.

### Detection in Movement Phase

**File:** `src/MakaMek.Core/Models/Game/Phases/MovementPhase.cs`

After the movement path is built, the movement phase checks each segment pair for the skid trigger. The pattern mirrors the existing water-entry detection:

1. Movement type is `Run`.
2. The **source** hex of the current segment `IsRoadOrPaved()`.
3. The **previous** step included a facing change (turn segment in the same hex, or the `TurnsTaken` counter advanced).
4. The current step enters a new hex.

On trigger:
- Create `SkidCheckRollContext(hexesMoved)`.
- Evaluate PSR via `FallProcessor` / `PilotingSkillCalculator` (existing infrastructure).
- On **failure**: emit `SegmentEvent(SegmentEventType.Skid, skidDistance)` where `skidDistance = ⌈hexesMoved ÷ 2⌉`.
- The fall processor handles the fall and skid slide, applying half normal falling damage per skid hex.

### Skid Damage

Skid damage is calculated as **half the standard falling damage** for each hex traveled during the skid. The existing `FallingDamageCalculator` is extended or wrapped to support a half-damage mode for skid hexes.

### To-Hit Modifiers

Two new `RollModifier` subclasses in `src/MakaMek.Core/Models/Game/Mechanics/Modifiers/Attack/`:

#### `SkiddingTargetModifier`

Applied to attacks **against** a unit that is currently in a skid state.

| Property | Value |
|----------|-------|
| Modifier value | `+2` |
| Condition | Target unit has `UnitStatus` skidding flag (or equivalent transient state) |

#### `SkiddingAttackerModifier`

Applied to attacks **by** a unit that is currently in a skid state.

| Property | Value |
|----------|-------|
| Modifier value | `+1` |
| Condition | Attacker unit has skidding flag |

Both modifiers follow the same pattern as existing `AttackerMovementModifier` / `TargetMovementModifier` and are collected in `ToHitCalculator`.

---

## Bridge Construction Factor

### CF Check on Entry

**File:** `src/MakaMek.Core/Models/Game/Phases/MovementPhase.cs`

When a unit steps onto a hex containing `BridgeTerrain`:

1. Sum the `Tonnage` of all units currently occupying that hex (including the entering unit).
2. Compare to `BridgeTerrain.ConstructionFactor`.
3. If total tonnage > CF:
   - Emit `SegmentEvent(SegmentEventType.BridgeCollapse, 0)` into the path.
   - Remove `BridgeTerrain` from the hex (bridge is destroyed).
   - All units on the hex fall to the underlying terrain; apply standard falling damage via `FallProcessor`.

`Unit.Tonnage` is already defined on the `Unit` base class.

### Network Propagation

`BridgeCollapse` is a `SegmentEventType` and propagates as a `SegmentEvent` within `PathSegmentData.Events`, consistent with the existing `Fall` event. No new command types are required.

---

## Data & Serialization

`TerrainData` is unchanged:

```csharp
public record TerrainData
{
    public required MakaMekTerrains Type { get; init; }
    public int? Height { get; init; }
}
```

Mapping:

| Terrain | `Type` | `Height` |
|---------|--------|---------|
| Road | `Road` | `null` |
| Pavement | `Pavement` | `null` |
| Bridge | `Bridge` | CF value (e.g., `60`) |

The MMTX terrain format document (`docs/architecture/MMTX-Terrain-Format.md`) must be updated to list the three new terrain types and the CF encoding for Bridge.

---

## Technical Design Summary

### Files to Modify

| File | Change |
|------|--------|
| `src/MakaMek.Map/Models/Terrains/MakaMekTerrains.cs` | Add `Road`, `Pavement`, `Bridge` values |
| `src/MakaMek.Map/Models/Terrains/Terrain.cs` | Add `case` entries in `FromData()` / `GetTerrainType()` |
| `src/MakaMek.Core/Data/Game/Mechanics/PilotingSkillRollType.cs` | Add `SkidCheck` |
| `src/MakaMek.Map/Models/SegmentEventType.cs` | Add `Skid`, `BridgeCollapse` |
| `src/MakaMek.Map/Models/BattleMap.cs` | On-road cost substitution in segment cost calculation |
| `src/MakaMek.Core/Models/Game/Phases/MovementPhase.cs` | Skid trigger detection; bridge CF check |
| `docs/architecture/MMTX-Terrain-Format.md` | Document new terrain types and CF encoding |

### Files to Create

| File | Purpose |
|------|---------|
| `src/MakaMek.Map/Models/Terrains/RoadTerrain.cs` | Road terrain implementation |
| `src/MakaMek.Map/Models/Terrains/PavementTerrain.cs` | Pavement terrain implementation |
| `src/MakaMek.Map/Models/Terrains/BridgeTerrain.cs` | Bridge terrain with `ConstructionFactor` |
| `src/MakaMek.Core/Data/Game/Mechanics/PilotingSkillRollContexts/SkidCheckRollContext.cs` | Skid PSR context record |
| `src/MakaMek.Core/Models/Game/Mechanics/Modifiers/Attack/SkiddingTargetModifier.cs` | +2 to-hit against skidding unit |
| `src/MakaMek.Core/Models/Game/Mechanics/Modifiers/Attack/SkiddingAttackerModifier.cs` | +1 to-hit penalty for skidding attacker |

`src/MakaMek.Map/Models/HexExtensions.cs` already exists; add the `IsRoadOrPaved()` extension method to it.

---

## Acceptance Criteria

### AC-1: Road/Pavement Terrain

- [ ] `Road`, `Pavement`, and `Bridge` appear in `MakaMekTerrains` enum.
- [ ] A hex containing `Road` over `HeavyWoods` costs **1 MP** to enter when moving road-to-road (not 3 MP).
- [ ] A unit moving from a non-road hex into a road hex pays the underlying terrain cost (not the road bonus).
- [ ] Elevation change MP costs are added even when on road.

### AC-2: Skid Detection

- [ ] A 'Mech on `Run` that changes facing on a `Road`/`Pavement` hex and then enters a new hex triggers a PSR.
- [ ] A 'Mech on `Walk` speed that changes facing on a road hex does **not** trigger a skid PSR.
- [ ] A 'Mech on `Run` that does **not** change facing on a road hex does **not** trigger a skid PSR.

### AC-3: Skid Outcome

- [ ] On PSR failure, the 'Mech falls and skids `⌈hexesMoved ÷ 2⌉` hexes.
- [ ] Skid damage = half normal falling damage per skid hex traversed.
- [ ] Attacking the skidding 'Mech applies a +2 to-hit modifier.
- [ ] The skidding 'Mech's own attacks apply a +1 to-hit penalty.

### AC-4: Bridge Construction Factor

- [ ] `BridgeTerrain` CF serializes to/from `TerrainData.Height`.
- [ ] When units on a bridge hex exceed its CF, the bridge collapses.
- [ ] Collapsed bridge hex has `BridgeTerrain` removed.
- [ ] Units on the collapsed hex fall to underlying terrain and take standard falling damage.

### AC-5: Serialization & Network

- [ ] `TerrainData` roundtrip preserves `Road`, `Pavement`, and `Bridge` (including CF).
- [ ] `Skid` and `BridgeCollapse` segment events propagate correctly over the network.
