# ToHitCalculator and PositionEvaluator Integration Analysis

**Date:** 2025-12-23  
**Status:** Analysis Complete - No Implementation

## Executive Summary

This analysis examines why `PositionEvaluator` (bot decision engine) cannot currently use `ToHitCalculator` (core game mechanics) and proposes architectural changes to enable code reuse. The primary barrier is a **state mismatch**: `ToHitCalculator` is designed for calculating actual attacks with current unit state, while `PositionEvaluator` needs to evaluate hypothetical attacks from positions units haven't moved to yet.

## 1. Current Implementation Analysis

### 1.1 ToHitCalculator (`MakaMek.Core/Models/Game/Mechanics/ToHitCalculator.cs`)

**Purpose:** Calculates to-hit numbers for weapon attacks using the GATOR system (Gunnery, Attacker movement, Target movement, Other modifiers, Range).

**Dependencies:**
- `IRulesProvider` (injected via constructor)

**Key Methods:**
- `GetToHitNumber()`: Returns total to-hit number
- `GetModifierBreakdown()`: Returns detailed breakdown with all modifiers

**Input Requirements:**
```csharp
public int GetToHitNumber(
    IUnit attacker,      // Must have Position, Pilot, MovementTypeUsed set
    IUnit target,        // Must have Position, DistanceCovered set
    Weapon weapon,
    IBattleMap map,
    bool isPrimaryTarget = true,
    PartLocation? aimedShotTarget = null)
```

**Modifiers Calculated:**
1. **Gunnery Base:** `attacker.Pilot.Gunnery`
2. **Attacker Movement:** From `attacker.MovementTypeUsed` (throws exception if null - line 72-74)
3. **Target Movement:** From `target.DistanceCovered`
4. **Range:** Based on weapon range brackets and distance
5. **Terrain:** Line-of-sight hexes between attacker and target
6. **Other Modifiers:**
   - Heat penalties (`attacker.GetAttackModifiers()`)
   - Prone firing penalty
   - Sensor damage
   - Arm actuator damage (shoulder, upper arm, lower arm)
   - Aimed shot modifier
   - Secondary target modifier

**Critical Constraints:**
- Line 44: Throws if `attacker.Pilot` is null
- Line 72-74: Throws if `attacker.MovementTypeUsed` is null
- Line 48: Assumes `attacker.Position` is set
- Line 60: Assumes weapon has `FirstMountPartLocation`
- Line 128: Calls `attacker.GetAttackModifiers(weaponLocation)` which depends on current unit state

### 1.2 PositionEvaluator (`MakaMek.Bots/Models/DecisionEngines/PositionEvaluator.cs`)

**Purpose:** Evaluates tactical positions for bot movement decisions based on defensive threat and offensive potential.

**Dependencies:**
- `IClientGame` (injected via constructor, provides access to `RulesProvider` and `BattleMap`)

**Key Methods:**
- `EvaluatePath()`: Evaluates a movement path with specific movement type
- `CalculateHitProbability()`: Estimates enemy hit probability (defensive evaluation)
- `CalculateHitProbabilityAsAttacker()`: Estimates friendly hit probability (offensive evaluation)

**Current Approach (Simplified Calculation):**
```csharp
// Lines 227-237 (defensive calculation)
var toHitNumber = attacker.Pilot?.Gunnery ?? 4;
toHitNumber += _game.RulesProvider.GetRangeModifier(range, weapon.LongRange, distance);
toHitNumber += _game.RulesProvider.GetTargetMovementModifier(targetHexesMoved);
if (attacker.MovementTypeUsed.HasValue)
{
    toHitNumber += _game.RulesProvider.GetAttackerMovementModifier(attacker.MovementTypeUsed.Value);
}
return DiceUtils.Calculate2d6Probability(toHitNumber);
```

**What's Missing:**
- ❌ Terrain modifiers (significant for tactical decisions)
- ❌ Heat penalties (important for overheated mechs)
- ❌ Prone penalties (important for fallen mechs)
- ❌ Sensor damage penalties
- ❌ Arm actuator damage penalties
- ❌ Secondary target penalties
- ❌ Aimed shot modifiers

**TODO Comments:**
- Line 57: `// TODO: evaluate how to use IHitCalculator for that`
- Line 216: `// TODO: ToHitCalculator should be updated to handle this case`

**Hypothetical Evaluation Challenge:**
- Evaluates positions where the unit **hasn't moved yet**
- Uses `HexPosition` (hypothetical destination) instead of `unit.Position` (current location)
- Uses `MovementType` parameter (planned movement) instead of `unit.MovementTypeUsed` (executed movement)
- Uses `hexesTraveled` parameter (hypothetical distance) instead of `target.DistanceCovered` (actual distance)

## 2. Integration Barriers

### 2.1 State Mismatch (Primary Barrier)

**The Core Problem:** `ToHitCalculator` expects units to be in their **actual current state** (position set, movement executed), while `PositionEvaluator` needs to evaluate **hypothetical future states** (positions not yet reached, movements not yet executed).

| Aspect | ToHitCalculator Expects | PositionEvaluator Needs |
|--------|------------------------|------------------------|
| Attacker Position | `attacker.Position` (current) | `HexPosition` (hypothetical) |
| Attacker Movement | `attacker.MovementTypeUsed` (executed) | `MovementType` (planned) |
| Target Distance | `target.DistanceCovered` (actual) | `hexesTraveled` (hypothetical) |
| Unit State | Current state with all modifiers | Projected state for evaluation |

### 2.2 Input/Output Mismatches

1. **Position Representation:**
   - ToHitCalculator: Uses `attacker.Position.Coordinates` (assumes unit has moved)
   - PositionEvaluator: Uses `HexPosition` (unit hasn't moved yet)

2. **Movement Type:**
   - ToHitCalculator: Reads from `attacker.MovementTypeUsed` (throws if null)
   - PositionEvaluator: Evaluates different movement types as parameters

3. **Attack Modifiers:**
   - ToHitCalculator: Calls `attacker.GetAttackModifiers(weaponLocation)` (current unit state)
   - PositionEvaluator: Cannot predict future unit state (heat might change, damage might occur)

### 2.3 Architectural Concerns

**Good News:** No architectural barriers exist.
- Dependency direction is correct: `MakaMek.Bots` → `MakaMek.Core`
- `ToHitCalculator` is in the Core layer (appropriate for shared logic)
- `PositionEvaluator` is in the Bots layer (appropriate for AI decisions)

**Concern:** `ToHitCalculator` is tightly coupled to `IUnit` interface and current game state.

### 2.4 Performance Considerations

- **ToHitCalculator** does more work:
  - Terrain checks via `map.GetHexesAlongLineOfSight()`
  - Unit state checks via `attacker.GetAttackModifiers()`
  - Multiple modifier calculations

- **PositionEvaluator** might call this hundreds of times per decision:
  - For each possible destination hex
  - For each enemy unit
  - For each weapon

- **Trade-off:** Accuracy vs. Speed
  - Missing modifiers (terrain, heat, damage) could lead to poor tactical decisions
  - Accurate calculations are worth the performance cost for better bot behavior

## 3. Refactoring Approaches

### 3.1 Option 1: Overload ToHitCalculator Methods

**Approach:** Add method overloads that accept explicit parameters instead of relying on unit state.

```csharp
public int GetToHitNumber(
    int attackerGunnery,
    HexCoordinates attackerPosition,
    MovementType attackerMovementType,
    HexCoordinates targetPosition,
    int targetHexesMoved,
    Weapon weapon,
    IBattleMap map,
    IReadOnlyList<RollModifier> attackerModifiers,
    bool isPrimaryTarget = true,
    PartLocation? aimedShotTarget = null)
```

**Pros:**
- ✅ Minimal changes to existing code
- ✅ Backward compatible
- ✅ Simple to implement

**Cons:**
- ❌ Large parameter list (10+ parameters)
- ❌ API becomes confusing with multiple overloads
- ❌ Doesn't solve `GetAttackModifiers()` issue cleanly
- ❌ Caller must manually extract and pass all parameters

### 3.2 Option 2: Extract Core Calculation Method

**Approach:** Create a private/internal method with all parameters explicit, used by both actual and hypothetical scenarios.

```csharp
private ToHitBreakdown CalculateBreakdown(
    int gunnery,
    HexCoordinates attackerPos,
    MovementType attackerMovement,
    HexCoordinates targetPos,
    int targetMoved,
    Weapon weapon,
    IBattleMap map,
    IReadOnlyList<RollModifier> otherModifiers,
    ...)
{
    // Core calculation logic
}

// Existing method delegates to core
public ToHitBreakdown GetModifierBreakdown(IUnit attacker, IUnit target, ...)
{
    var modifiers = GetDetailedOtherModifiers(attacker, target, ...);
    return CalculateBreakdown(
        attacker.Pilot.Gunnery,
        attacker.Position.Coordinates,
        attacker.MovementTypeUsed.Value,
        target.Position.Coordinates,
        target.DistanceCovered,
        weapon,
        map,
        modifiers,
        ...);
}
```

**Pros:**
- ✅ Clean separation of concerns
- ✅ Reusable core logic
- ✅ Backward compatible

**Cons:**
- ❌ Still has large parameter list
- ❌ Caller must handle `GetAttackModifiers()` separately
- ❌ Internal method is complex

### 3.3 Option 3: Create AttackScenario Abstraction (RECOMMENDED)

**Approach:** Encapsulate all attack parameters in a context object that can represent both actual and hypothetical attacks.

```csharp
public record AttackScenario
{
    public required int AttackerGunnery { get; init; }
    public required HexCoordinates AttackerPosition { get; init; }
    public required HexCoordinates TargetPosition { get; init; }
    public required MovementType AttackerMovementType { get; init; }
    public required int TargetHexesMoved { get; init; }
    public required IReadOnlyList<RollModifier> AttackerModifiers { get; init; }
    public HexDirection? AttackerFacing { get; init; }
    public HexDirection? TargetFacing { get; init; }
    public bool IsPrimaryTarget { get; init; } = true;
    public PartLocation? AimedShotTarget { get; init; }
    
    // Factory method for actual attacks
    public static AttackScenario FromUnits(
        IUnit attacker,
        IUnit target,
        PartLocation weaponLocation,
        bool isPrimaryTarget = true,
        PartLocation? aimedShotTarget = null)
    {
        if (attacker.Pilot is null)
            throw new Exception("Attacker pilot is not assigned");
        if (attacker.MovementTypeUsed is null)
            throw new Exception("Attacker's Movement Type is undefined");
            
        return new AttackScenario
        {
            AttackerGunnery = attacker.Pilot.Gunnery,
            AttackerPosition = attacker.Position!.Coordinates,
            TargetPosition = target.Position!.Coordinates,
            AttackerMovementType = attacker.MovementTypeUsed.Value,
            TargetHexesMoved = target.DistanceCovered,
            AttackerModifiers = attacker.GetAttackModifiers(weaponLocation),
            AttackerFacing = attacker is Mech mech ? mech.TorsoDirection : attacker.Position.Facing,
            TargetFacing = target.Position.Facing,
            IsPrimaryTarget = isPrimaryTarget,
            AimedShotTarget = aimedShotTarget
        };
    }
    
    // Factory method for hypothetical attacks (bot evaluation)
    public static AttackScenario FromHypothetical(
        int attackerGunnery,
        HexCoordinates attackerPosition,
        MovementType attackerMovementType,
        HexCoordinates targetPosition,
        int targetHexesMoved,
        IReadOnlyList<RollModifier> attackerModifiers,
        HexDirection? attackerFacing = null,
        HexDirection? targetFacing = null,
        bool isPrimaryTarget = true)
    {
        return new AttackScenario
        {
            AttackerGunnery = attackerGunnery,
            AttackerPosition = attackerPosition,
            TargetPosition = targetPosition,
            AttackerMovementType = attackerMovementType,
            TargetHexesMoved = targetHexesMoved,
            AttackerModifiers = attackerModifiers,
            AttackerFacing = attackerFacing,
            TargetFacing = targetFacing,
            IsPrimaryTarget = isPrimaryTarget
        };
    }
}
```

**ToHitCalculator Refactoring:**
```csharp
// New method accepting scenario
public ToHitBreakdown GetModifierBreakdown(
    AttackScenario scenario,
    Weapon weapon,
    IBattleMap map)
{
    var distance = scenario.AttackerPosition.DistanceTo(scenario.TargetPosition);
    var range = weapon.GetRangeBracket(distance);
    var hasLos = map.HasLineOfSight(scenario.AttackerPosition, scenario.TargetPosition);
    
    var terrainModifiers = GetTerrainModifiers(scenario.AttackerPosition, scenario.TargetPosition, map);
    
    // Build modifiers list
    var otherModifiers = new List<RollModifier>(scenario.AttackerModifiers);
    
    // Add aimed shot if applicable
    if (scenario.AimedShotTarget.HasValue)
    {
        otherModifiers.Add(new AimedShotModifier
        {
            TargetLocation = scenario.AimedShotTarget.Value,
            Value = _rules.GetAimedShotModifier(scenario.AimedShotTarget.Value)
        });
    }
    
    // Add secondary target modifier if applicable
    if (!scenario.IsPrimaryTarget && scenario.AttackerFacing.HasValue)
    {
        var isInFrontArc = scenario.AttackerPosition.IsInFiringArc(
            scenario.TargetPosition,
            scenario.AttackerFacing.Value,
            FiringArc.Front);
        otherModifiers.Add(new SecondaryTargetModifier
        {
            IsInFrontArc = isInFrontArc,
            Value = _rules.GetSecondaryTargetModifier(isInFrontArc)
        });
    }
    
    return new ToHitBreakdown
    {
        GunneryBase = new GunneryRollModifier { Value = scenario.AttackerGunnery },
        AttackerMovement = new AttackerMovementModifier
        {
            Value = _rules.GetAttackerMovementModifier(scenario.AttackerMovementType),
            MovementType = scenario.AttackerMovementType
        },
        TargetMovement = new TargetMovementModifier
        {
            Value = _rules.GetTargetMovementModifier(scenario.TargetHexesMoved),
            HexesMoved = scenario.TargetHexesMoved
        },
        OtherModifiers = otherModifiers,
        RangeModifier = new RangeRollModifier
        {
            Value = _rules.GetRangeModifier(range, weapon.LongRange, distance),
            Range = range,
            Distance = distance,
            WeaponName = weapon.Name
        },
        TerrainModifiers = terrainModifiers,
        HasLineOfSight = hasLos
    };
}

// Existing method for backward compatibility
public ToHitBreakdown GetModifierBreakdown(
    IUnit attacker,
    IUnit target,
    Weapon weapon,
    IBattleMap map,
    bool isPrimaryTarget = true,
    PartLocation? aimedShotTarget = null)
{
    var weaponLocation = weapon.FirstMountPartLocation ?? 
        throw new Exception($"Weapon {weapon.Name} is not mounted");
    var scenario = AttackScenario.FromUnits(attacker, target, weaponLocation, isPrimaryTarget, aimedShotTarget);
    return GetModifierBreakdown(scenario, weapon, map);
}
```

**PositionEvaluator Usage:**
```csharp
private double CalculateHitProbabilityAsAttacker(
    IUnit attacker,
    IUnit target,
    Weapon weapon,
    HexPosition attackerPosition,
    MovementType movementType)
{
    if (_game.BattleMap == null || target.Position == null)
        return 0;
    
    // Get current attack modifiers from the unit
    // (heat, prone, sensors, arm actuators based on current state)
    var attackerModifiers = attacker.GetAttackModifiers(
        weapon.FirstMountPartLocation ?? PartLocation.CenterTorso);
    
    // Create hypothetical scenario
    var scenario = AttackScenario.FromHypothetical(
        attackerGunnery: attacker.Pilot?.Gunnery ?? 4,
        attackerPosition: attackerPosition.Coordinates,
        attackerMovementType: movementType,
        targetPosition: target.Position.Coordinates,
        targetHexesMoved: target.DistanceCovered,
        attackerModifiers: attackerModifiers,
        attackerFacing: attackerPosition.Facing,
        targetFacing: target.Position.Facing);
    
    // Use ToHitCalculator with full accuracy
    var toHitNumber = _game.ToHitCalculator.GetToHitNumber(scenario, weapon, _game.BattleMap);
    
    return DiceUtils.Calculate2d6Probability(toHitNumber);
}
```

**Pros:**
- ✅ Clean, self-documenting API
- ✅ Single source of truth for calculation logic
- ✅ Supports both actual and hypothetical scenarios
- ✅ Backward compatible (existing methods still work)
- ✅ Extensible (easy to add new scenario types)
- ✅ Testable (can create scenarios without full unit setup)
- ✅ Clear separation of concerns
- ✅ Includes ALL modifiers (terrain, heat, damage, etc.)

**Cons:**
- ⚠️ Requires new types (`AttackScenario`)
- ⚠️ More complex initial implementation
- ⚠️ Need to update existing call sites (can be done gradually)

### 3.4 Option 4: Create Separate TacticalToHitCalculator

**Approach:** Create a simplified calculator specifically for bot use.

**Pros:**
- ✅ Simple, focused implementation
- ✅ No risk to existing code
- ✅ Can optimize for bot performance

**Cons:**
- ❌ Code duplication
- ❌ Divergence over time (rules changes need to be applied twice)
- ❌ Still missing important modifiers
- ❌ Violates DRY principle

## 4. Recommendation

**Adopt Option 3: AttackScenario Abstraction**

This approach provides the best balance of:
- **Code Reuse:** Single source of truth for to-hit calculations
- **Flexibility:** Supports both actual and hypothetical scenarios
- **Accuracy:** Bots get access to ALL modifiers (terrain, heat, damage)
- **Maintainability:** Changes to rules only need to be made once
- **Backward Compatibility:** Existing code continues to work
- **Testability:** Easy to create test scenarios

### Implementation Steps

1. **Create `AttackScenario` record** in `MakaMek.Core/Data/Game/Mechanics/`
   - Add factory methods for actual and hypothetical scenarios
   - Include all necessary parameters

2. **Refactor `ToHitCalculator`**
   - Add new method accepting `AttackScenario`
   - Refactor existing methods to use `AttackScenario` internally
   - Keep existing public API for backward compatibility

3. **Update `PositionEvaluator`**
   - Replace simplified calculations with `ToHitCalculator` calls
   - Use `AttackScenario.FromHypothetical()` factory method
   - Remove TODO comments

4. **Add Tests**
   - Test `AttackScenario` factory methods
   - Test `ToHitCalculator` with hypothetical scenarios
   - Test `PositionEvaluator` with full modifier support

5. **Gradual Migration**
   - Existing code continues to work
   - New code uses `AttackScenario` approach
   - Gradually migrate call sites as needed

## 5. Impact Analysis

### Benefits

1. **Better Bot Decisions:**
   - Bots will consider terrain when evaluating positions
   - Bots will account for heat penalties
   - Bots will avoid positions where they're prone
   - Bots will consider sensor/actuator damage

2. **Code Quality:**
   - Eliminates code duplication
   - Single source of truth for game rules
   - Easier to maintain and test

3. **Consistency:**
   - Bots use same calculation logic as actual combat
   - Rules changes automatically apply to bots

### Risks

1. **Performance:**
   - More calculations per position evaluation
   - Mitigation: Profile and optimize if needed, accuracy is worth it

2. **Complexity:**
   - New abstraction to learn
   - Mitigation: Good documentation and examples

3. **Migration Effort:**
   - Need to update existing call sites
   - Mitigation: Can be done gradually, backward compatible

## 6. Conclusion

The primary barrier preventing `PositionEvaluator` from using `ToHitCalculator` is the **state mismatch** between actual attacks (current unit state) and hypothetical attacks (future positions). The recommended solution is to introduce an `AttackScenario` abstraction that can represent both cases, enabling code reuse while maintaining clean separation of concerns.

This refactoring will significantly improve bot decision-making by giving bots access to the full range of combat modifiers, leading to more realistic and tactically sound behavior.

---

**Next Steps:** If approved, create implementation tasks for the refactoring approach.

