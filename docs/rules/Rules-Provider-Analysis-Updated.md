# Game Rules Architecture Analysis (Updated)

## Overview

This document analyzes the architectural inconsistency in how the MakaMek codebase handles game rule values and provides recommendations for standardization, with special consideration for the Open-Closed Principle.

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

## Critical Issue: Open-Closed Principle Violation

**Problem Identified**: The centralized rules provider approach violates the Open-Closed Principle for entity extension.

### Current Extension Requirements

**Adding a new terrain type currently requires:**

**Hardcoded Approach:**
1. Add enum value to `MakaMekTerrains`
2. Create new terrain class (e.g., `SwampTerrain.cs`)
3. Update factory method in `Terrain.GetTerrainType()`

**Pure Rules Provider Approach would require:**
1. All of the above PLUS
2. Modify `ClassicBattletechRulesProvider.GetTerrainToHitModifier()`
3. Potentially modify other rule methods

**This breaks the Open-Closed Principle** - the system should be open for extension but closed for modification.

## Revised Recommendation: Hybrid Approach

**Decision**: Adopt a **Hybrid Approach** that combines the benefits of both patterns while maintaining extensibility.

### Hybrid Architecture Design

1. **Entity-Level Defaults**: Terrain classes contain default rule values (maintaining Open-Closed compliance)
2. **Rules Provider Overrides**: Rules provider can override defaults for specific rule sets
3. **Fallback Mechanism**: System falls back to entity defaults when rules provider doesn't specify values

### Implementation Strategy

#### 1. Enhanced IRulesProvider Interface

```csharp
public interface IRulesProvider
{
    // Existing methods...
    
    // New optional override methods
    int? GetTerrainMovementCostOverride(MakaMekTerrains terrainType);
    int? GetTerrainHeightOverride(MakaMekTerrains terrainType);
    int? GetTerrainInterveningFactorOverride(MakaMekTerrains terrainType);
}
```

#### 2. Enhanced Terrain Base Class

```csharp
public abstract class Terrain
{
    protected IRulesProvider? _rulesProvider;
    
    public abstract MakaMekTerrains Id { get; }
    
    // Default values defined in derived classes
    protected abstract int DefaultHeight { get; }
    protected abstract int DefaultInterveningFactor { get; }
    protected abstract int DefaultMovementCost { get; }
    
    // Public properties with rules provider fallback
    public int Height => _rulesProvider?.GetTerrainHeightOverride(Id) ?? DefaultHeight;
    public int InterveningFactor => _rulesProvider?.GetTerrainInterveningFactorOverride(Id) ?? DefaultInterveningFactor;
    public int MovementCost => _rulesProvider?.GetTerrainMovementCostOverride(Id) ?? DefaultMovementCost;
    
    public void SetRulesProvider(IRulesProvider rulesProvider)
    {
        _rulesProvider = rulesProvider;
    }
}
```

#### 3. Updated Terrain Implementation

```csharp
public class ClearTerrain : Terrain
{
    public override MakaMekTerrains Id => MakaMekTerrains.Clear;
    protected override int DefaultHeight => 0;
    protected override int DefaultInterveningFactor => 0;
    protected override int DefaultMovementCost => 1;
}

// New terrain types can be added without modifying existing code
public class SwampTerrain : Terrain
{
    public override MakaMekTerrains Id => MakaMekTerrains.Swamp;
    protected override int DefaultHeight => 0;
    protected override int DefaultInterveningFactor => 1;
    protected override int DefaultMovementCost => 3;
}
```

#### 4. Rules Provider Implementation

```csharp
public class ClassicBattletechRulesProvider : IRulesProvider
{
    // Existing methods...
    
    public int? GetTerrainMovementCostOverride(MakaMekTerrains terrainType)
    {
        // Only override if different from default
        return terrainType switch
        {
            MakaMekTerrains.HeavyWoods => 4, // Override default of 3
            _ => null // Use terrain default
        };
    }
    
    public int? GetTerrainHeightOverride(MakaMekTerrains terrainType) => null; // Use defaults
    public int? GetTerrainInterveningFactorOverride(MakaMekTerrains terrainType) => null; // Use defaults
}
```

### Benefits of Hybrid Approach

| Aspect | Hybrid Approach |
|--------|----------------|
| **Open-Closed Principle** | ✅ New terrain types require no modification of existing code |
| **Multiple Rule Sets** | ✅ Rules provider can override defaults for different variants |
| **Backward Compatibility** | ✅ Existing terrain classes work unchanged |
| **Testability** | ✅ Can mock rules provider and test with different rule sets |
| **Performance** | ✅ Minimal overhead - only checks override when rules provider is set |
| **Maintainability** | ✅ Clear separation between defaults and rule-specific overrides |

### Migration Path

1. **Phase 1**: Implement hybrid terrain system
2. **Phase 2**: Update terrain factory to inject rules provider
3. **Phase 3**: Apply pattern to other entity types (weapons, equipment, etc.)
4. **Phase 4**: Create alternative rules providers for different BattleTech variants

## Conclusion

The hybrid approach provides the best of both worlds:
- **Maintains extensibility** for adding new entities without code modification
- **Supports multiple rule sets** through optional overrides
- **Preserves performance** with minimal overhead
- **Enables comprehensive testing** through dependency injection

This architectural decision positions MakaMek for long-term success while respecting fundamental software design principles.

---

**Document Version**: 2.0  
**Date**: 2025-07-09  
**Author**: Augment Agent  
**Status**: Updated Recommendation
