# Bot TurnState Caching Architecture Analysis

## 1. Executive Summary

This document analyzes the introduction of a `BotTurnState` caching mechanism to optimize the `MakaMek.Bots` decision-making process. The primary goal is to eliminate redundant target evaluations between the [MovementEngine](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/MovementEngine.cs#22-27) and [WeaponsEngine](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/WeaponsEngine.cs#25-30) by caching results for units that have completed their movement.

## 2. Current Architecture & Problem

### Current Flow
1. **Movement Phase**: [MovementEngine](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/MovementEngine.cs#22-27) evaluates hundreds of potential paths for each friendly unit. For each path, it calls `TacticalEvaluator.EvaluateTargets` to assess offensive potential against all enemies.
2. **Weapons Phase**: [WeaponsEngine](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/WeaponsEngine.cs#25-30) selects an attacker and calls `TacticalEvaluator.EvaluateTargets` again to determine the best firing solution.

### The Problem
- [EvaluateTargets](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/TacticalEvaluator.cs#55-114) is computationally expensive (raycasting, hit probability calculations).
- Calculations performed during [MovementEngine](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/MovementEngine.cs#22-27)'s evaluation of the *chosen* path are effectively discarded.
- [WeaponsEngine](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/WeaponsEngine.cs#25-30) recalculates the exact same data (same unit, same position, same targets) that was likely calculated seconds earlier.

## 3. Proposed Solution: BotTurnState

### 3.1. TurnState Structure

We propose a new class `BotTurnState` that persists across the phases of a single turn for a bot instance.

```csharp
public class BotTurnState
{
    public Guid GameId { get; }
    public int TurnNumber { get; }
    
    // Key: (AttackerId, AttackerPositionDetails, TargetId, TargetPositionDetails)
    // Value: Cached evaluation data
    private readonly Dictionary<TargetEvaluationKey, IReadOnlyList<TargetEvaluationData>> _targetEvaluationCache = new();

    public BotTurnState(Guid gameId, int turnNumber)
    {
        GameId = gameId;
        TurnNumber = turnNumber;
    }

    public bool TryGet(TargetEvaluationKey key, out IReadOnlyList<TargetEvaluationData> data) 
    { 
        return _targetEvaluationCache.TryGetValue(key, out data); 
    }

    public void Add(TargetEvaluationKey key, IReadOnlyList<TargetEvaluationData> data) 
    {
        // Concurrency handling required if engines run in parallel
        lock (_targetEvaluationCache) 
        {
            _targetEvaluationCache[key] = data;
        }
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

**Key properties of this key structure:**
- **Attacker/Target Position & Facing**: Ensures that even if a unit hasn't "moved" in the game sense, any change in position or facing invalidates the cache key automatically.
- **Unit IDs**: Uniquely identifies the combatants.

### 3.2. Cache Invalidation Strategy

The cache needs to be cleared when the game state changes significantly enough that cross-phase caching is no longer valid.

- **Turn Transitions**: The cache **MUST** be cleared (or a new `BotTurnState` instance created) at the start of a new turn.
- **Phase Transitions**: The cache **SHOULD BE PRESERVED** between [Movement](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/MovementEngine.cs#22-27) and [Weapons](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/WeaponsEngine.cs#25-30) phases to achieve the optimization goal.
- **Dynamic Updates**: Because the cache key includes the *exact coordinates and facing* of both attacker and target, the cache automatically handles units moving.
    - If [MovementEngine](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/MovementEngine.cs#22-27) evaluates `UnitA@Hex1` vs `EnemyB@Hex2`:
        - If `EnemyB` has *already moved* to `Hex2`, this entry is valid for the [Weapons](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/WeaponsEngine.cs#25-30) phase.
        - If `EnemyB` has *not moved* (is at `Hex2`) but later moves to `Hex3`:
            - In [Weapons](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/WeaponsEngine.cs#25-30) phase, we query `UnitA@Hex1` vs `EnemyB@Hex3`.
            - The cache key (..., `Hex3`, ...) will result in a **MISS**.
            - Correctness is maintained without explicit manual invalidation of specific units.

## 4. Integration Points

### 4.1. Bot Class Updates
The [Bot](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/Bot.cs#27-50) class will own the lifecycle of `BotTurnState`.

```csharp
public class Bot : IBot 
{
    private BotTurnState? _currentTurnState;
    
    // ...

    private void HandleGameCommand(object command)
    {
        // ...
        if (command is TurnIncrementedCommand turnCmd)
        {
            // Reset state on new turn
            _currentTurnState = new BotTurnState(_clientGame.Id, turnCmd.TurnNumber);
        }
    }
    
    // ...
    private async Task MakeDecision()
    {
        // Ensure state exists (lazy init if needed)
        _currentTurnState ??= new BotTurnState(_clientGame.Id, _clientGame.TurnNumber);
        
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
    Task MakeDecision(IPlayer player, BotTurnState turnState);
}
```

### 4.3. TacticalEvaluator Updates
Update [EvaluateTargets](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/TacticalEvaluator.cs#55-114) to use the cache.

```csharp
public ValueTask<IReadOnlyList<TargetEvaluationData>> EvaluateTargets(
    IUnit attacker, 
    MovementPath attackerPath, 
    IReadOnlyList<IUnit> potentialTargets,
    BotTurnState? turnState = null) // Optional to support existing calls/tests
{
    // ...
    var validTargets = new List<TargetEvaluationData>();
    
    foreach (var target in potentialTargets)
    {
        // Construct Key
        var key = new TargetEvaluationKey(
            attacker.Id, attackerPath.Destination.Coordinates, attackerPath.Destination.Facing,
            target.Id, target.Position.Coordinates, target.Position.Facing
        );
        
        // Check Cache
        if (turnState?.TryGet(key, out var cachedData) == true)
        {
            validTargets.AddRange(cachedData);
            continue;
        }
        
        // Calculate
        // ... calculation logic ...
        
        // Store
        turnState?.Add(key, resultForThisTarget);
    }
    // ...
}
```

**Note**: The current [EvaluateTargets](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/TacticalEvaluator.cs#55-114) returns a list for *all* targets. The caching logic logic suggested above requires breaking this down or caching the *entire* list if the input list matches.
**Refined Strategy**: Since `GetReachableHexes` or other logic might pass different subsets of enemies, caching per-unit-pair (Attacker+Target) is more robust than caching the whole list result. [EvaluateTargets](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/TacticalEvaluator.cs#55-114) should iterate targets, check cache for each, calculate misses, and return the aggregated list.

## 5. Performance Considerations

- **Memory Overhead**:
    - Assuming 100 paths * 20 targets = 2000 pairs per movement decision.
    - Each entry ~150-200 bytes.
    - Total per turn ~ 400KB - 1MB.
    - This is negligible for modern systems.
- **Concurrency**: [MovementEngine](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/MovementEngine.cs#22-27) uses `Parallel.ForEachAsync`. `BotTurnState` must use a thread-safe collection (e.g., `ConcurrentDictionary`) or lock on write. `ConcurrentDictionary` is recommended.

## 6. Testing Strategy

### 6.1. Unit Tests
- **TurnStateTests**: specific tests for the key generation and storage logic.
- **TacticalEvaluatorTests**:
    - Add test: `EvaluateTargets_ShouldUseCachedResult_WhenAvailable`.
    - Add test: `EvaluateTargets_ShouldCacheNewResult_WhenNotAvailable`.
    - Add test: `EvaluateTargets_ShouldMissCache_WhenTargetPositionChanges` (Simulates enemy moving later).

### 6.2. Integration Tests
- **BotTests**: Verify that [MakeDecision](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/MovementEngine.cs#28-102) passes the same `BotTurnState` instance across different engine calls within the same turn.

## 7. Alternative Approaches Considered

- **Global/Singleton Cache**: Rejected. Hard to manage lifecycle and clean up after games/tests. Encapsulating in `BotTurnState` owned by [Bot](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/Bot.cs#27-50) is cleaner.
- **Caching in Engine**: Rejected. [MovementEngine](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/MovementEngine.cs#22-27) and [WeaponsEngine](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/WeaponsEngine.cs#25-30) are separate instances (likely) or at least logically distinct. They need a shared medium.
- **Caching only "Final" Move**: We could try to only cache the evaluation of the path we *actually* took.
    - *Pros*: Saves memory.
    - *Cons*: Harder to implement. [MovementEngine](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/MovementEngine.cs#22-27) calculates priorities *after* evaluation. We don't know which path is "final" until after we've done the work. Caching everything is safer and simpler given the low memory cost.

## 8. Implementation Plan Checklist

1. [ ] Create `BotTurnState` class and `TargetEvaluationKey` struct.
2. [ ] Update [IBotDecisionEngine](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/IBotDecisionEngine.cs#8-17) signature.
3. [ ] Update [Bot](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/Bot.cs#27-50) to manage `BotTurnState` lifecycle.
4. [ ] Refactor `MakaMek.Bots` project to update all [MakeDecision](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/MovementEngine.cs#28-102) implementations (fix build errors).
5. [ ] Update `ITacticalEvaluator` and [TacticalEvaluator](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/TacticalEvaluator.cs#16-327) to accept `BotTurnState`.
6. [ ] Implement caching logic in [TacticalEvaluator](file:///c:/Users/anton/myrepos/MakaMek/src/MakaMek.Bots/Models/DecisionEngines/TacticalEvaluator.cs#16-327).
7. [ ] Update tests.

