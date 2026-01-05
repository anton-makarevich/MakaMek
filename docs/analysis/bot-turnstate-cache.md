# Bot TurnState Caching Architecture Analysis

## 1. Executive Summary

This document analyzes the introduction of a `TurnState` caching mechanism to optimize the `MakaMek.Bots` decision-making process. The primary goal is to eliminate redundant target evaluations between the [MovementEngine](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/MovementEngine.cs#22-27) and [WeaponsEngine](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/WeaponsEngine.cs#16-235) by caching results for units that have completed their movement.

## 2. Current Architecture & Problem

### Current Flow
1. **Movement Phase**: [MovementEngine](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/MovementEngine.cs#22-27) evaluates hundreds of potential paths for each friendly unit. For each path, it calls `TacticalEvaluator.EvaluateTargets` to assess offensive potential against all enemies.
2. **Weapons Phase**: [WeaponsEngine](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/WeaponsEngine.cs#16-235) selects an attacker and calls `TacticalEvaluator.EvaluateTargets` again to determine the best firing solution.

### The Problem
- [EvaluateTargets](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/TacticalEvaluator.cs#55-114) is computationally expensive (raycasting, hit probability calculations).
- Calculations performed during [MovementEngine](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/MovementEngine.cs#22-27)'s evaluation of the *chosen* path are effectively discarded.
- [WeaponsEngine](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/WeaponsEngine.cs#16-235) recalculates the exact same data (same unit, same position, same targets) that was likely calculated seconds earlier.

## 3. Proposed Solution: TurnState

### 3.1. TurnState Structure

We propose a new class `TurnState` that persists across the phases of a single turn for a bot instance. It will use a `ConcurrentDictionary` to safely handle parallel evaluations from the [MovementEngine](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/MovementEngine.cs#22-27).

```csharp
public class TurnState
{
    public Guid GameId { get; }
    public int TurnNumber { get; }
    
    // Key: (AttackerId, AttackerPositionDetails, TargetId, TargetPositionDetails)
    // Value: Cached evaluation data (List of target evaluation data for that specific pair)
    private readonly ConcurrentDictionary<TargetEvaluationKey, IReadOnlyList<TargetEvaluationData>> _targetEvaluationCache = new();

    public TurnState(Guid gameId, int turnNumber)
    {
        GameId = gameId;
        TurnNumber = turnNumber;
    }

    public bool TryGetTargetEvaluation(TargetEvaluationKey key, out IReadOnlyList<TargetEvaluationData> data) 
    { 
        return _targetEvaluationCache.TryGetValue(key, out data); 
    }

    public void AddTargetEvaluation(TargetEvaluationKey key, IReadOnlyList<TargetEvaluationData> data) 
    {
        _targetEvaluationCache.TryAdd(key, data);
    }
}

public readonly record struct TargetEvaluationKey(
    Guid AttackerId, 
    HexCoordinates AttackerCoords, 
    HexDirection AttackerFacing,
    Guid TargetId,
    HexCoordinates TargetCoords,
    HexDirection TargetFacing
);
```

**Key properties:**
- **ConcurrentDictionary**: Ensures thread-safety during parallel path evaluations in [MovementEngine](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/MovementEngine.cs#22-27).
- **Per-Pair Granularity**: The cache stores the evaluation result for a single Attacker-Target pair. This is critical because [EvaluateTargets](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/TacticalEvaluator.cs#55-114) is often called with different subsets of enemies or in different contexts. Caching the entire list result of [EvaluateTargets](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/TacticalEvaluator.cs#55-114) would be fragile; caching per-pair is robust.

### 3.2. Cache Invalidation Strategy

The cache needs to be cleared when the game state changes significantly enough that cross-phase caching is no longer valid.

- **Turn Transitions**: The cache **MUST** be cleared (or a new `TurnState` instance created) at the start of a new turn.
- **Phase Transitions**: The cache **SHOULD BE PRESERVED** between [Movement](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/MovementEngine.cs#22-27) and [Weapons](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/WeaponsEngine.cs#180-213) phases to achieve the optimization goal.
- **Dynamic Updates**: Because the cache key includes the *exact coordinates and facing* of both attacker and target, the cache automatically handles units moving.
    - If [MovementEngine](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/MovementEngine.cs#22-27) evaluates `UnitA@Hex1` vs `EnemyB@Hex2`:
        - If `EnemyB` has *already moved* to `Hex2`, this entry is valid for the [Weapons](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/WeaponsEngine.cs#180-213) phase.
        - If `EnemyB` has *not moved* (is at `Hex2`) but later moves to `Hex3`:
            - In [Weapons](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/WeaponsEngine.cs#180-213) phase, we query `UnitA@Hex1` vs `EnemyB@Hex3`.
            - The cache key (..., `Hex3`, ...) will result in a **MISS**.
            - Correctness is maintained without explicit manual invalidation of specific units.

## 4. Integration Points

### 4.1. Bot Class Updates
The [Bot](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/Bot.cs#13-132) class will own the lifecycle of `TurnState`.

```csharp
public class Bot : IBot 
{
    private ITurnState? _currentTurnState;
    
    // ...

    private void HandleGameCommand(object command)
    {
        // ...
        if (command is TurnIncrementedCommand turnCmd)
        {
            // Reset state on new turn
            _currentTurnState = new TurnState(_clientGame.Id, turnCmd.TurnNumber);
        }
    }
    
    // ...
    private async Task MakeDecision()
    {
        // Ensure state exists (lazy init if needed)
        _currentTurnState ??= new TurnState(_clientGame.Id, _clientGame.TurnNumber);
        
        // Pass to engine
        await _currentDecisionEngine.MakeDecision(player, _currentTurnState);
    }
}
```

### 4.2. Decision Engine Interface
Update [IBotDecisionEngine](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/IBotDecisionEngine.cs#8-17) to accept the state.

```csharp
public interface IBotDecisionEngine
{
    Task MakeDecision(IPlayer player, ITurnState turnState);
}
```

### 4.3. TacticalEvaluator Updates
Update [EvaluateTargets](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/TacticalEvaluator.cs#55-114) to use the cache. It will iterate through the provided potential targets, check the cache for each pair, and only calculate the missing ones.

```csharp
public ValueTask<IReadOnlyList<TargetEvaluationData>> EvaluateTargets(
    IUnit attacker, 
    MovementPath attackerPath, 
    IReadOnlyList<IUnit> potentialTargets,
    ITurnState? turnState = null)
{
    var allResults = new List<TargetEvaluationData>();
    
    foreach (var target in potentialTargets)
    {
        // Construct Key
        var key = new TargetEvaluationKey(
            attacker.Id, attackerPath.Destination.Coordinates, attackerPath.Destination.Facing,
            target.Id, target.Position.Coordinates, target.Position.Facing
        );
        
        IReadOnlyList<TargetEvaluationData> evaluationForOneTarget;

        // Check Cache
        if (turnState?.TryGetTargetEvaluation(key, out var cachedData) == true)
        {
            evaluationForOneTarget = cachedData;
        }
        else
        {
            // Calculate for this single target
            // Note: The current internal logic of EvaluateTargets might need refactoring 
            // to support single-target evaluation efficiently, or we just extract the logic loop.
            evaluationForOneTarget = CalculateForSingleTarget(attacker, attackerPath, target);
            
            // Store
            turnState?.AddTargetEvaluation(key, evaluationForOneTarget);
        }

        allResults.AddRange(evaluationForOneTarget);
    }
    
    return ValueTask.FromResult<IReadOnlyList<TargetEvaluationData>>(allResults);
}
```

## 5. Performance Considerations

- **Memory Overhead**:
    - Assuming 100 paths * 20 targets = 2000 pairs per movement decision.
    - Each entry ~150-200 bytes.
    - Total per turn ~ 400KB - 1MB.
    - This is negligible.
- **Concurrency**: `TurnState` uses `ConcurrentDictionary`, ensuring safe access during [MovementEngine](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/MovementEngine.cs#22-27)'s parallel path evaluation.

## 6. Testing Strategy

### 6.1. Unit Tests
- **TurnStateTests**: Verify `ConcurrentDictionary` behavior and key equality logic.
- **TacticalEvaluatorTests**:
    - Add test: `EvaluateTargets_ShouldUseCachedResult_WhenAvailable`.
    - Add test: `EvaluateTargets_ShouldCacheNewResult_WhenNotAvailable`.
    - Add test: `EvaluateTargets_ShouldMissCache_WhenTargetPositionChanged`.

### 6.2. Integration Tests
- **BotTests**: Verify that [MakeDecision](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/WeaponsEngine.cs#31-138) passes the same `TurnState` instance across different engine calls within the same turn.

## 7. Alternative Approaches Considered

- **Global/Singleton Cache**: Rejected. Hard to manage lifecycle.
- **Caching in Engine**: Rejected. Engines are stateless or scoped to phase.
- **Per-List Caching**: Rejected. [EvaluateTargets](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/TacticalEvaluator.cs#55-114) inputs vary too much. Per-pair caching is robust and reusable.

## 8. Implementation Plan Checklist

1. [ ] Create `TurnState` class (+ `ITurnState` to simplify mocking in unit tests) and `TargetEvaluationKey` struct.
2. [ ] Update [IBotDecisionEngine](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/IBotDecisionEngine.cs#8-17) signature.
3. [ ] Update [Bot](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/Bot.cs#13-132) to manage `TurnState` lifecycle.
4. [ ] Refactor `MakaMek.Bots` project to update all [MakeDecision](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/WeaponsEngine.cs#31-138) implementations.
5. [ ] Update `ITacticalEvaluator` and [TacticalEvaluator](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/TacticalEvaluator.cs#20-24) to accept `TurnState`.
6. [ ] Implement caching logic in [TacticalEvaluator](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/TacticalEvaluator.cs#20-24) (extracting single-target eval logic).
7. [ ] Update tests to cover caching scenarios.

