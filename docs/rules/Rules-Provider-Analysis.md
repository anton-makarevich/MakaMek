# Game Rules Architecture Analysis

## Overview

This document analyzes the architectural inconsistency in how the MakaMek codebase handles game rule values and provides recommendations for standardization.

## Current State: Two Approaches

### 1. Hardcoded Values Approach (Terrain System)

**Location**: `src/MakaMek.Core/Models/Map/Terrains/`

**Implementation**: Each terrain class directly contains its game rule values as hardcoded properties.

```csharp
public class ClearTerrain : Terrain
{
    public override MakaMekTerrains Id => MakaMekTerrains.Clear;
    public override int Height => 0;
    public override int InterveningFactor => 0;
    public override int MovementCost => 1;
}
```

**Characteristics**:
- Values embedded in class definitions
- No external dependencies
- Direct property access

### 2. Centralized Rules Provider Approach (Piloting Skill System)

**Location**: `src/MakaMek.Core/Utils/TechRules/ClassicBattletechRulesProvider.cs`

**Implementation**: Game rule values are centralized in a rules provider with dependency injection.

```csharp
public int GetPilotingSkillRollModifier(PilotingSkillRollType psrType)
{
    return psrType switch
    {
        PilotingSkillRollType.GyroHit => 3,
        PilotingSkillRollType.GyroDestroyed => 6,
        PilotingSkillRollType.LowerLegActuatorHit => 1,
        // ...
    };
}
```

**Characteristics**:
- Centralized rule management
- Dependency injection through `IRulesProvider`
- Clear method signatures organized by rule type

## Detailed Comparison

### Maintainability

| Aspect | Hardcoded Approach | Centralized Rules Provider |
|--------|-------------------|---------------------------|
| **Rule Changes** | ❌ Requires modifying multiple classes | ✅ Single location to update |
| **Consistency** | ❌ Risk of inconsistent values across classes | ✅ Guaranteed consistency |
| **Code Duplication** | ❌ Similar logic scattered across classes | ✅ No duplication |
| **Debugging** | ❌ Must search multiple files for rule issues | ✅ Single source of truth |

### Testability

| Aspect | Hardcoded Approach | Centralized Rules Provider |
|--------|-------------------|---------------------------|
| **Unit Testing** | ❌ Cannot mock rule values for edge cases | ✅ Easy to mock `IRulesProvider` |
| **Test Isolation** | ❌ Tests coupled to hardcoded values | ✅ Tests can use custom rule sets |
| **Rule Validation** | ❌ Must test each terrain class separately | ✅ Comprehensive rule testing in one place |

### Extensibility (Rule Set Support)

| Aspect | Hardcoded Approach | Centralized Rules Provider |
|--------|-------------------|---------------------------|
| **Multiple Rule Sets** | ❌ Would require separate terrain class hierarchies | ✅ Simply implement different `IRulesProvider` |
| **Runtime Rule Switching** | ❌ Impossible without major refactoring | ✅ Swap provider implementations |
| **Custom Rules** | ❌ Requires creating new terrain classes | ✅ Create custom provider implementation |
| **Mod Support** | ❌ Very difficult to implement | ✅ Natural extension point |

### Performance

| Aspect | Hardcoded Approach | Centralized Rules Provider |
|--------|-------------------|---------------------------|
| **Memory Usage** | ✅ Minimal - values embedded in classes | ⚠️ Slightly higher - provider instance |
| **Access Speed** | ✅ Direct property access | ⚠️ Method call overhead |
| **Initialization** | ✅ No setup required | ⚠️ DI container setup |

### Code Organization

| Aspect | Hardcoded Approach | Centralized Rules Provider |
|--------|-------------------|---------------------------|
| **Separation of Concerns** | ❌ Business logic mixed with data | ✅ Clear separation |
| **Domain Modeling** | ⚠️ Terrain behavior coupled to rules | ✅ Terrain behavior independent |
| **Dependency Management** | ✅ No external dependencies | ⚠️ Requires DI setup |

## Recommendation: Standardize on Centralized Rules Provider

**Decision**: Adopt the **Centralized Rules Provider approach** throughout the MakaMek codebase.

### Primary Justifications

1. **Future-Proofing for Multiple Rule Sets**: MakaMek will likely need to support different BattleTech rule variants or house rules. The centralized approach makes this trivial.

2. **Superior Maintainability**: Having all game rules in one location dramatically reduces maintenance burden and eliminates consistency issues.

3. **Enhanced Testability**: The ability to mock rule providers enables comprehensive testing of edge cases and rule interactions.

4. **Professional Architecture**: The centralized approach follows established enterprise patterns and separation of concerns principles.

### Implementation Strategy

1. **Create Terrain Rules Provider Methods**: Add methods like `GetTerrainMovementCost()`, `GetTerrainHeight()`, `GetTerrainInterveningFactor()` to `IRulesProvider`

2. **Refactor Terrain Classes**: Remove hardcoded values and inject `IRulesProvider` dependency

3. **Maintain Backward Compatibility**: Keep existing terrain class interfaces while changing internal implementation

4. **Gradual Migration**: Apply this pattern to other hardcoded rule areas (weapon stats, heat values, etc.)

### Example Refactored Implementation

```csharp
public class ClearTerrain : Terrain
{
    private readonly IRulesProvider _rules;
    
    public ClearTerrain(IRulesProvider rules)
    {
        _rules = rules;
    }
    
    public override MakaMekTerrains Id => MakaMekTerrains.Clear;
    public override int Height => _rules.GetTerrainHeight(Id);
    public override int InterveningFactor => _rules.GetTerrainInterveningFactor(Id);
    public override int MovementCost => _rules.GetTerrainMovementCost(Id);
}
```

### Performance Considerations

While the centralized approach has slight performance overhead, this is negligible in the context of a turn-based strategy game. The benefits far outweigh the minimal performance cost.

## Conclusion

The centralized rules provider approach provides superior maintainability, testability, and extensibility. This architectural decision will position MakaMek for long-term success with multiple rule sets, easier maintenance, and more robust testing capabilities.

## Next Steps

1. Extend `IRulesProvider` interface with terrain-related methods
2. Update `ClassicBattletechRulesProvider` implementation
3. Refactor terrain classes to use dependency injection
4. Update terrain factory and dependency injection configuration
5. Migrate other hardcoded rule areas using the same pattern

---

**Document Version**: 1.0  
**Date**: 2025-07-09  
**Author**: Augment Agent  
**Status**: Recommendation
