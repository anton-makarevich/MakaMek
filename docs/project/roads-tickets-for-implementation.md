# Roads, Pavement & Bridges — Implementation Tickets

> Cross-referenced against: `docs/rules/roads-and-bridges.md`, existing codebase at `src/`
> Generated: 2026-06-01

## Ticket Dependency Graph

```
T01   T02   T03   T04   T05   T06   T07   T08   T09   T10   T11
 │     │     │     │     │     │     │     │     │     │     │
 └──┬──┘     │     │     └──┬──┘     │     │     │     │     │
    │        │     │        │        │     │     │     │     │
    ▼        ▼     ▼        ▼        │     │     │     │     │
   T12 ──► T13 ──► T14 ◄─── T15      │     │     │     │     │
    │                                │     │     │     │     │
    ▼                                ▼     │     │     │     │
   T16                               T17   │     │     │     │
    │                                │     │     │     │     │
    ▼                                ▼     ▼     ▼     ▼     ▼
                                 T18 ◄── T19 ── T20 ── T21 ── T22
                                                      │
                                                      ▼
                                                     T23
```

---

## [T01] Add `Road`, `Pavement`, `Bridge` to `MakaMekTerrains` enum

**File:** `src/MakaMek.Map/Models/Terrains/MakaMekTerrains.cs`

**Value:** Foundation — all other terrain work depends on these enum values.

**Task:**
- Add three new values to the `MakaMekTerrains` enum: `Road`, `Pavement`, `Bridge`.

**Testable by:** Unit test asserts `MakaMekTerrains.Road`, `Pavement`, `Bridge` exist and have distinct int values.

**Dependencies:** None.

**Open Questions:** None.

---

## [T02] Create `RoadTerrain`, `PavementTerrain`, `BridgeTerrain` classes

**Files:** 
- `src/MakaMek.Map/Models/Terrains/RoadTerrain.cs` (new)
- `src/MakaMek.Map/Models/Terrains/PavementTerrain.cs` (new)
- `src/MakaMek.Map/Models/Terrains/BridgeTerrain.cs` (new)

**Value:** Concrete terrain implementations matching BattleTech rules. BridgeTerrain is the first structural terrain with ConstructionFactor.

**Task:**
- `RoadTerrain`: `Id=Road`, `Height=0`, `InterveningFactor=0`, `MovementCost=1`
- `PavementTerrain`: `Id=Pavement`, `Height=0`, `InterveningFactor=0`, `MovementCost=1`
- `BridgeTerrain`: `Id=Bridge`, `Height` (constructor param, positive int for bridge surface height above hex base), `InterveningFactor=0`, `MovementCost=1`, `ConstructionFactor` (int, max supported tonnage). Override `ToData()` to serialize both `Height` and `ConstructionFactor`.

All follow the same pattern as `WaterTerrain`, `ClearTerrain`, etc.

**Testable by:** Unit tests for each class verifying all property values. `BridgeTerrain.ToData()` roundtrip test.

**Dependencies:** T01.

---

## [T03] Update `MakaMekTerrains` source generator references (if any)

**Note:** No explicit source generator tracks MakaMekTerrains enum. The `MovementCostTypeResolverGenerator` handles MovementCost subtypes, not terrain. Check if any partial method on Terrain needs update. No changes expected.

**Testable by:** Build succeeds without errors.

**Dependencies:** None.

---

## [T04] Add `ConstructionFactor` field to `TerrainData` record

**File:** `src/MakaMek.Map/Data/TerrainData.cs`

**Value:** Enables serialization of structural terrain properties (bridges, future buildings).

**Task:**
- Add `public int? ConstructionFactor { get; init; }` to the `TerrainData` record. Null for non-structural terrains.

**Testable by:** Unit test: create TerrainData with ConstructionFactor=60, verify property roundtrips. Test that existing terrains (Clear, Woods, etc.) roundtrip with null CF.

**Dependencies:** None.

**Open Questions:**
- Q1: Should `ConstructionFactor` have a default? PRD says null for non-structural. This is clean.

---

## [T05] Extend `Terrain.GetTerrainType()` and `FromData()` for new terrain types

**File:** `src/MakaMek.Map/Models/Terrains/Terrain.cs`

**Value:** Factory can produce the new terrain types from data, enabling deserialization and editor creation.

**Task:**
- Add `case` entries in `GetTerrainType()` for `Road`, `Pavement`, `Bridge`.
- `Road -> new RoadTerrain()`, `Pavement -> new PavementTerrain()`.
- `Bridge` needs both `height` and `ConstructionFactor` from `TerrainData`. Current factory signature only accepts `int? height`.
- **Approach:** In `FromData()`, handle `Bridge` specially after calling `GetTerrainType()`:
  ```csharp
  if (data.Type == MakaMekTerrains.Bridge && data.ConstructionFactor.HasValue)
      ((BridgeTerrain)terrain).ConstructionFactor = data.ConstructionFactor.Value;
  ```
  Or add an optional `int? constructionFactor` parameter to `GetTerrainType()`.

**Testable by:** Unit tests: create `TerrainData` for each new type, call `FromData()`, verify type and properties. Bridge roundtrip with Height and CF.

**Dependencies:** T01, T02, T04.

**Open Questions:**
- Q2 (blocking): `GetTerrainType()` currently has signature `(MakaMekTerrains, int? height = null)`. Adding CF parameter is a breaking change to the public API. Option: Add optional param `int? constructionFactor = null`. This is the simplest approach but slightly ugly for non-Bridge terrains. Alternative: refactor to accept `TerrainData` directly.

---

## [T06] Add `SkidCheck` to `PilotingSkillRollType` enum

**File:** `src/MakaMek.Core/Data/Game/Mechanics/PilotingSkillRollType.cs`

**Value:** Foundation for skid PSR mechanics.

**Task:**
- Add `SkidCheck` value to the enum (the existing comment `// Skid` at line 50 suggests the planned location).

**Testable by:** Unit test asserts `PilotingSkillRollType.SkidCheck` exists.

**Dependencies:** None.

---

## [T07] Add `Skid` and `BridgeCollapse` to `SegmentEventType` enum

**File:** `src/MakaMek.Map/Models/SegmentEventType.cs`

**Value:** Foundation for segment-event propagation of skid and bridge collapse.

**Task:**
- Add `Skid` and `BridgeCollapse` values to the enum.

**Testable by:** Unit test asserts the new values exist.

**Dependencies:** None.

---

## [T08] Create `SkidCheckRollContext` record

**File:** `src/MakaMek.Core/Data/Game/Mechanics/PilotingSkillRollContexts/SkidCheckRollContext.cs` (new)

**Value:** Carries hexes-moved data for skid PSR evaluation and distance calculation.

**Task:**
- Record inheriting `PilotingSkillRollContext(RollType = PilotingSkillRollType.SkidCheck)`:
  ```csharp
  public record SkidCheckRollContext(int HexesMoved)
      : PilotingSkillRollContext(PilotingSkillRollType.SkidCheck);
  ```
- Override `Render()` to display skid check info.
- The source generator `PilotingSkillRollContextTypeResolverGenerator` auto-discovers this by inheritance — no manual registration needed.

**Testable by:** Unit tests verifying record creation, RollType, HexesMoved, and Render() output.

**Dependencies:** T06.

---

## [T09] Add `IsRoadOrPaved()` extension method to `HexExtensions`

**File:** `src/MakaMek.Map/Models/HexExtensions.cs`

**Value:** Centralized "on road" detection used by movement cost, skid trigger, and bridge check logic.

**Task:**
- Add using the existing `extension(Hex hex)` syntax:
  ```csharp
  public bool IsRoadOrPaved()
      => hex.HasTerrain(MakaMekTerrains.Road)
      || hex.HasTerrain(MakaMekTerrains.Pavement)
      || hex.HasTerrain(MakaMekTerrains.Bridge);
  ```

**Testable by:** Unit tests: hex with Road → true, hex with Pavement → true, hex with Bridge → true, hex with Clear → false, hex with LightWoods → false.

**Dependencies:** T01, T02.

---

## [T10] Add `Road` value to `TerrainAssetType` enum

**File:** `src/MakaMek.Assets/Models/Terrains/TerrainAssetType.cs`

**Value:** Foundation for road/bridge bitmask texture retrieval.

**Task:**
- Add `Road` value.

**Testable by:** Unit test asserts `TerrainAssetType.Road` exists.

**Dependencies:** None.

---

## [T11] Add `GetRoadTextureImage` to `ITerrainAssetService` and implement in `TerrainCachingService`

**Files:**
- `src/MakaMek.Assets/Services/ITerrainAssetService.cs`
- `src/MakaMek.Assets/Services/TerrainCachingService.cs`

**Value:** Enables road/bridge bitmask texture loading from MMTX packages (`terrains/road/` directory).

**Task:**
- Interface: Add `Task<byte[]?> GetRoadTextureImage(string biomeId, CanonicalBitmaskResult canonicalBitmask)`.
- Implementation: Mirror `GetWaterTextureImage()` — convert canonical mask to 6-bit binary string, look up in `terrains/road/{bitmask}.png` within MMTX archives, apply variant selection.

**Testable by:** Unit tests with mock MMTX stream containing `terrains/road/000001.png`. Verify texture returned for known bitmask. Verify null for missing bitmask.

**Dependencies:** T10.

**Open Questions:**
- Q3: Should Pavement hexes participate in road bitmask connectivity? The PRD says "Road and Bridge neighbors both count toward road connectivity" but doesn't mention Pavement. If Pavement is a broad area (not a strip), it likely should NOT create road connections. If the intent is otherwise, `ComputeCanonicalBitmask` would need to check multiple terrain types. **Recommendation:** For v1, only Road and Bridge count for bitmask connectivity. Pavement hexes use the isolated texture.

---

## [T12] Implement on-road cost substitution in `BattleMap.ConvertPathToSegments()`

**File:** `src/MakaMek.Map/Models/BattleMap.cs`

**Value:** Core gameplay — units on roads pay 1 MP regardless of underlying terrain.

**Task:**
- In `ConvertPathToSegments()`, when a movement segment enters a new hex:
  1. Check if **both** `fromHex` and `toHex` satisfy `IsRoadOrPaved()` (need to add `using Sanet.MakaMek.Map.Models.Terrains` for the enum).
  2. If on-road, substitute the `TerrainMovementCost.Value` with `1` (road surface cost).
  3. Elevation change cost is still added on top.
  4. The `TerrainId` in the movement cost record stays as the underlying terrain (e.g., HeavyWoods). This is a deliberate simplification per PRD.
- **Note:** The pathfinding A* algorithm does NOT account for on-road cost reduction. It still calculates using `hex.MovementCost` (max of all terrains). This is a known limitation accepted in the PRD.

**Testable by:**
- Unit test: hex with Road+HeavyWoods, fromHex and toHex both road/paved → segment cost = 1 + elevation.
- Unit test: fromHex is Clear, toHex is Road → cost = underlying terrain cost (not road bonus).
- Unit test: road+woods hex with elevation change → cost = 1 + |elevationChange|.

**Dependencies:** T02 (terrain classes), T09 (IsRoadOrPaved).

**Open Questions:**
- Q4: When on-road with HeavyWoods underlying, the `TerrainMovementCost.TerrainId` is HeavyWoods but value is 1. Is this semantically OK? The PRD says yes: "Hex.MovementCost property itself is not changed." If the downstream consumers need to display the correct terrain name, this could be confusing. Consider whether `TerrainId` should be `Road` when on-road.
- Q5 (known limitation): Pathfinding (A*) doesn't know about road cost reduction. This means the shortest path may not utilize road benefits. It's acceptable for v1 but should be documented.

---

## [T13] Add road/bridge bitmask rendering to `HexControl`

**Files:**
- `src/MakaMek.Avalonia/MakaMek.Avalonia.Controls/HexControl.cs`
- `src/MakaMek.Avalonia/MakaMek.Avalonia/Views/BattleMapView.axaml.cs`

**Value:** Visual rendering of road/bridge networks using bitmask-based textures.

**Task:**
1. In `HexControl.cs`:
   - Add `private const int ZIndexRoadLayer = 25;` (between overlays at 20+ and polygon at 30).
   - Add `private readonly CanonicalBitmaskResult? _roadBitmask;` field.
   - Accept `CanonicalBitmaskResult? roadBitmask = null` in constructor.
   - Add `UpdateRoadLayer()` method mirroring `UpdateWaterLayer()`:
     - Check `_roadBitmask != null`
     - Call `_terrainAssetService.GetRoadTextureImage(biomeId, _roadBitmask)`
     - Render at `ZIndexRoadLayer` with `-_roadBitmask.RotationSteps * 60.0` rotation.
   - Call `UpdateRoadLayer()` in `Render()` after `UpdateWaterLayer()`.
2. In `BattleMapView.axaml.cs` `RenderMap()`:
   - Compute `_roadBitmask` when hex contains Road or Bridge terrain:
     ```csharp
     CanonicalBitmaskResult? roadBitmask = null;
     if (bitmaskService != null && game.BattleMap != null 
         && (hex.HasTerrain(MakaMekTerrains.Road) || hex.HasTerrain(MakaMekTerrains.Bridge)))
     {
         roadBitmask = bitmaskService.ComputeCanonicalBitmask(
             game.BattleMap, hex.Coordinates, MakaMekTerrains.Road);
     }
     ```
   - Pass `roadBitmask` to `HexControl` constructor.

**Testable by:** Visual/automated test: hex with Road and LightWoods renders in order: base → light woods → road. Hex with no road neighbors uses isolated texture. Hex connected to 6 road neighbors uses fully-connected texture.

**Dependencies:** T07 (SegmentEventType — not really needed here, but for understanding event flow), T09 (IsRoadOrPaved), T11 (GetRoadTextureImage), T12 (ConvertPathToSegments — not really needed, but movement cost integration).

---

## [T14] Create skid to-hit modifier records

**Files:**
- `src/MakaMek.Core/Models/Game/Mechanics/Modifiers/Attack/SkiddingTargetModifier.cs` (new)
- `src/MakaMek.Core/Models/Game/Mechanics/Modifiers/Attack/SkiddingAttackerModifier.cs` (new)

**Value:** To-hit modifiers for skidding units (+2 against, +1 for own attacks).

**Task:**
- Both follow the `AttackerMovementModifier`/`TargetMovementModifier` pattern (record inheriting `RollModifier`).
- `SkiddingTargetModifier`: `Value = +2`, `Render()` uses `"Modifier_SkiddingTarget"` key.
- `SkiddingAttackerModifier`: `Value = +1`, `Render()` uses `"Modifier_SkiddingAttacker"` key.
- Source generator `RollModifierTypeResolverGenerator` auto-discovers by inheritance — no manual registration.

**Testable by:** Unit tests verifying Value, Render() output with mock localization service.

**Dependencies:** None (self-contained records).

---

## [T15] Add skidding transient state to `Unit` / `Mech`

**File:** `src/MakaMek.Core/Models/Units/Unit.cs` (or `Mech.cs`)

**Value:** Enables to-hit modifier system to check whether a unit is currently skidding.

**Task:**
- Add a transient bool property to `Unit`: `public bool IsSkidding { get; set; }` (default false, not persisted).
- The property is set when a skid SegmentEvent triggers and cleared when the unit finishes its movement or the phase ends.

**Testable by:** Unit tests: default is false, can set true/false.

**Dependencies:** None.

**Open Questions:**
- Q6: How is skidding state wired into the to-hit modifier system? The `ToHitCalculator.GetDetailedOtherModifiers()` builds modifiers from `scenario.AttackerModifiers`. Currently `AttackScenario` populates `AttackerModifiers` from `unit.GetAttackModifiers()`. The skidding modifiers need to be included there when `IsSkidding` is true. The PRD doesn't specify this integration path. **Recommendation:** Add skidding modifier collection in `Mech.GetAttackModifiers()` (or a similar hook point), or add them in `ToHitCalculator.GetDetailedOtherModifiers()` by checking the scenario's attacker/target state.
- Q7: When is IsSkidding cleared? After the unit's movement completes? After resolve attacks? Need to define lifecycle.

---

## [T16] Implement skid trigger detection in `MovementPhase`

**File:** `src/MakaMek.Core/Models/Game/Phases/MovementPhase.cs`

**Value:** Core gameplay — 'Mechs running on pavement risk skidding when turning.

**Task:**
- In `ProcessMoveCommand()`, after the path is built, check each segment pair for skid trigger:
  1. Movement type is `Run`.
  2. The **source** hex of the current movement segment `IsRoadOrPaved()`.
  3. The **previous** segment was a turn (same coordinates, different facing → check `TurnsTaken` or previous segment type).
  4. The current segment enters a new hex.
- On trigger:
  - Count total hexes moved so far (build from completed segments).
  - Create `SkidCheckRollContext(hexesMoved)`.
  - Call `FallProcessor.ProcessMovementAttempt()` with the context.
  - On PSR failure: emit `SegmentEvent(SegmentEventType.Skid, skidDistance)` where `skidDistance = ⌈hexesMoved ÷ 2⌉`.
  - Set `unit.IsSkidding = true`.
  - The fall processor handles the fall and applies half damage per skid hex (see T17).

**Pattern:** Mirrors existing `FindWaterEntrySegments()` → PSR fallback flow.

**Testable by:**
- Unit: Run movement + facing change on road → PSR triggered.
- Unit: Walk movement + facing change on road → no PSR.
- Unit: Run movement + no facing change on road → no PSR.
- Unit: Run + facing change on Clear hex → no PSR.

**Dependencies:** T06 (SkidCheck PSR type), T07 (Skid SegmentEvent), T08 (SkidCheckRollContext), T09 (IsRoadOrPaved).

---

## [T17] Implement skid damage (half falling damage per skid hex)

**File:** `src/MakaMek.Core/Models/Game/Mechanics/Mechs/Falling/FallingDamageCalculator.cs` (and possibly `FallProcessor`)

**Value:** Skidding Mechs take half normal falling damage per hex skidded.

**Task:**
- Extend `IFallingDamageCalculator` (or create a wrapper) with a method for skid damage:
  ```csharp
  FallingDamageData CalculateSkidDamage(Mech mech, int skidHexes)
  ```
- Skid damage = `ceil(mech.Tonnage / 10.0) * 0.5 * skidHexes` per skid hex (half the per-level falling damage of `ceil(tonnage/10)` per hex).
- Or add a bool parameter to `CalculateFallingDamage` for half-damage mode.
- In `FallProcessor`, when processing a skid fall, call the skid damage method instead of the standard falling damage method.

**Testable by:** Unit test: 20-ton Mech skids 4 hexes → standard falling damage per hex = 2, half = 1, total = 4 damage vs normal fall of 1 level = 2 damage.

**Dependencies:** T16 (skid detection calls this).

---

## [T18] Implement bridge CF check and collapse in `MovementPhase`

**File:** `src/MakaMek.Core/Models/Game/Phases/MovementPhase.cs`

**Value:** Core gameplay — bridges collapse when units exceed weight capacity.

**Task:**
- In `ProcessMoveCommand()`, when a unit steps onto a hex containing `BridgeTerrain`:
  1. Sum the `Tonnage` of all units currently occupying that hex (including the entering unit).
     - Need a way to query units on a hex. Check if `IBattleMap` or `IGame` has a unit-location query. If not, this needs infrastructure.
  2. Compare to `BridgeTerrain.ConstructionFactor`.
  3. If total tonnage > CF:
     - Emit `SegmentEvent(SegmentEventType.BridgeCollapse, 0)` into the path.
     - Remove `BridgeTerrain` from the hex (call `hex.RemoveTerrain(MakaMekTerrains.Bridge)`).
     - All units on the hex fall to the underlying terrain; apply standard falling damage via `FallProcessor`.
     - The falling damage: levels = bridge height (the distance to the terrain below).

**Actual BT rules clarification from roads-and-bridges.md:** "If the total tonnage of units on a bridge hex exceeds its current CF, the bridge collapses, and the units take standard falling damage to the terrain below."

**Testable by:**
- Unit: 40-ton Mech on hex with Bridge CF=60 → no collapse.
- Unit: 40-ton + 30-ton on hex with Bridge CF=60 → collapse.
- Unit: Collapsed hex has BridgeTerrain removed.
- Unit: Units on collapsed hex receive falling damage.

**Dependencies:** T02 (BridgeTerrain), T07 (BridgeCollapse SegmentEvent), T09 (IsRoadOrPaved).

**Open Questions:**
- Q8: How to query units on a hex? The current `IBattleMap` / `IGame` doesn't have a `GetUnitsOnHex()` method. Need to add one or iterate all units.
- Q9: Collapse removes BridgeTerrain — is this one hex at a time or the entire bridge? BT rules say individual bridge hexes collapse. When a hex collapses, the rest of the bridge remains intact.
- Q10: `SegmentEvent.BridgeCollapse` — The `SegmentEvent` record struct is just `record struct SegmentEvent(SegmentEventType Type)`. For BridgeCollapse, does it need additional data (like "which CF it had")? The PRD's `BridgeCollapse` segment event propagates with just the type. The actual collapse side effects (removing terrain, falling damage) happen server-side in MovementPhase before the event is emitted.

---

## [T19] Implement under-bridge clearance calculation and enforcement

**File:** `src/MakaMek.Map/Models/BattleMap.cs` (and/or movement validation)

**Value:** Prevents tall units from moving under low bridges.

**Task:**
- Add a method (or integrate into `ConvertPathToSegments` or movement path validation) to check bridge clearance:
  ```csharp
  clearance = BridgeTerrain.Height - min(t.Height for all non-bridge terrains in hex)
  ```
  Example: hex level 0, water (Height=-1), bridge (Height=1) → clearance = 1 - (-1) = 2 levels.
- A unit can move underneath when `unit.Height ≤ clearance`.
- When clearance is insufficient, the bridge hex is treated as a building hex — the unit cannot enter from below (via underlying terrain).
- The check applies specifically when a unit is NOT traveling on the road (i.e., moving on underlying terrain into a hex that has a bridge).
- Unit height is already on `Mech`: `Height = 2` (standing) / `1` (prone) from `Mech.cs`.

**Testable by:**
- Hex at level 0, water depth -1, bridge Height=1 → clearance = 2. Standing Mech (height 2) can pass under.
- Same hex, but a hypothetical unit with height 3 → cannot pass under.
- Unit moving road-to-road on bridge → clearance check does NOT apply (unit is on top, not underneath).

**Dependencies:** T02 (BridgeTerrain), T09 (IsRoadOrPaved).

**Open Questions:**
- Q11: Where exactly does the clearance check go? Options: (a) in pathfinding's level-change validation, (b) in `ConvertPathToSegments`, (c) in `MovementPhase.ProcessMoveCommand()`. Best fit is probably in `MovementPhase` — when processing each non-road segment into a bridge hex, validate clearance before allowing the move.
- Q12: Does the PRD intend clearance to be checked by the client (path UI) or only server-side? For good UX it should prevent selection of invalid hexes in path UI.

---

## [T20] Add localization strings for new types and modifiers

**File:** `tests/MakaMek.Localization.Tests/FakeLocalizationServiceTests.cs` (and corresponding resource files)

**Value:** Enables rendering of new terrain names, PSR types, and modifiers in the UI.

**Task:**
- Add entries:
  - `"Terrain_Road" → "Road"`
  - `"Terrain_Pavement" → "Pavement"`
  - `"Terrain_Bridge" → "Bridge"`
  - `"PilotingSkillRollType_SkidCheck" → "Skid Check"` (or similar)
  - `"PilotingSkillRollType_SkidCheck_WithHexes" → "{0} ({1} hexes)"` (if the context record uses a formatted render)
  - `"Modifier_SkiddingTarget" → "Target Skidding: +{0}"`
  - `"Modifier_SkiddingAttacker" → "Attacker Skidding: +{0}"`

**Testable by:** Existing FakeLocalizationServiceTests pattern.

**Dependencies:** T06, T08, T14.

---

## [T21] Update `MMTX-Terrain-Format.md` documentation

**File:** `docs/architecture/MMTX-Terrain-Format.md`

**Value:** Documents the new terrain types and road bitmask directory for asset creators.

**Task:**
- Add `road` to the list of overlay types: `Road, Pavement, Bridge` terrain types.
- Document the new `terrains/road/` bitmask directory with 6-bit binary naming (same as `terrains/water/`).
- Document optional `terrains/road/` directory in the bundle structure tree.
- Document Bridge Height and ConstructionFactor encoding in TerrainData.

**Testable by:** Manual review.

**Dependencies:** T01, T02, T04, T10, T11.

---

## [T22] Wire skidding modifiers into the to-hit calculation system

**File:** `src/MakaMek.Core/Models/Game/Mechanics/ToHitCalculator.cs` (and/or `Mech.GetAttackModifiers()`)

**Value:** Skidding modifiers actually apply during attack resolution.

**Task:**
- In `ToHitCalculator.GetDetailedOtherModifiers()`:
  - Check if `scenario.TargetUnit.IsSkidding` → add `SkiddingTargetModifier`.
  - Check if `scenario.AttackerUnit.IsSkidding` → add `SkiddingAttackerModifier`.
- Or alternatively, add skidding modifiers to `Mech.GetAttackModifiers()` and let them flow through `AttackScenario.AttackerModifiers`.

**Note:** The target modifier is trickier since `GetAttackModifiers()` is on the attacker. The target-side modifier needs to be added by the calculator.

**Testable by:** Integration test: attacker skidding → to-hit number includes +1 penalty. Target skidding → to-hit number includes +2 bonus for attacker.

**Dependencies:** T14 (modifier records), T15 (IsSkidding state).

---

## [T23] End-to-end integration tests

**Value:** Validates the complete feature set works together.

**Task:**
- Test: unit moves road-to-road through heavy woods → 1 MP per hex.
- Test: unit runs on road + facing change → skid PSR triggered.
- Test: bridge collapse when combined tonnage exceeds CF.
- Test: `TerrainData` roundtrip for all three new types including Bridge CF.
- Test: `Skid` and `BridgeCollapse` segment events propagate via `PathSegmentData.Events`.

**Dependencies:** All prior tickets.

---

## Open Questions Summary

| # | Question | Affected Tickets |
|---|----------|-----------------|
| Q1 | Default for ConstructionFactor? PRD says null for non-structural. OK. | T04 |
| Q2 | **Blocking:** `GetTerrainType()` only takes `int? height`. Bridge also needs `ConstructionFactor`. Solution: add optional param `int? constructionFactor = null` or refactor to accept `TerrainData`. | T05 |
| Q3 | Should Pavement count for road bitmask connectivity? | T11, T13 |
| Q4 | On-road, `TerrainMovementCost.TerrainId` is HeavyWoods but value is 1. Is this confusing for UI? | T12 |
| Q5 | Pathfinding doesn't use road cost reduction. Documented limitation. | T12 |
| Q6 | How should skidding modifiers integrate with to-hit calculation — via `Mech.GetAttackModifiers()` or `ToHitCalculator.GetDetailedOtherModifiers()`? | T15, T22 |
| Q7 | When is `IsSkidding` cleared? After movement? After attacks? | T15 |
| Q8 | No `GetUnitsOnHex()` method exists. Need to add or iterate all units. | T18 |
| Q9 | Bridge collapse is per-hex or entire bridge? PRD: per hex. | T18 |
| Q10 | `SegmentEvent` has no data payload beyond Type. Is this sufficient for BridgeCollapse? | T07, T18 |
| Q11 | Where does under-bridge clearance check live? | T19 |
| Q12 | Should clearance be validated client-side (path UI) or only server-side? | T19 |

---

## Cross-Check Summary

### PRD vs Rules Document (`docs/rules/roads-and-bridges.md`)
✅ All rules from the rules document are covered in the PRD scope. No discrepancies found.

### PRD vs Existing Codebase
✅ All existing patterns are followed. Source generators auto-discover new types. Key findings:
- ⚠️ Pathfinding will not use road cost reduction (known limitation, scoped out of v1)
- ⚠️ `BridgeTerrain` serialization needs factory signature change (Q2)
- ⚠️ No unit-on-hex query exists yet (Q8)
- ⚠️ Skidding state lifecycle needs explicit design (Q6, Q7)
- ⚠️ Pavement bitmask connectivity needs clarification (Q3)
