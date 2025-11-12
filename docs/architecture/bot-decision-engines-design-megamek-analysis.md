# Bot Decision Engines Design - MegaMek Princess Analysis

**Date:** 2025-11-12
**Status:** Design Document
**Based on:** MegaMek Princess bot analysis, bot-player-system-prd.md, bot-player-system-implementation-roadmap.md

---

## Executive Summary

This document analyzes the MegaMek Princess bot implementation and provides specific, actionable recommendations for implementing MakaMek's DecisionEngines. The Princess bot is a sophisticated AI system that has evolved over many years, and while we cannot directly port its Java code, we can learn from its architectural patterns and decision-making strategies.

**Key Takeaway:** Princess uses a **utility-based decision system** combined with **configurable behavior settings** to create flexible, strategic bot players. MakaMek should adopt similar patterns while leveraging its existing C# architecture and command pattern.

---

## 1. MegaMek Princess Architecture Overview

### 1.1 Core Components

Princess is structured around several key components:

1. **Princess.java** - Main bot class extending BotClient
   - Manages PathRankers (movement evaluation)
   - Manages FireControls (weapon targeting)
   - Maintains BehaviorSettings (personality configuration)
   - Runs Precognition thread (enemy prediction)
   - Tracks HeatMaps (tactical positioning)

2. **BehaviorSettings.java** - Configurable bot personality
   - Self-preservation index (0-10): How much to avoid damage
   - Fall shame index (0-10): How much to avoid piloting skill failures
   - Hyper aggression index (0-10): How close to get to enemies
   - Herd mentality index (0-10): How much to stick with teammates
   - Bravery index (0-10): How quickly to flee when damaged
   - Strategic targets: Buildings/objectives to prioritize
   - Priority targets: Specific enemy units to focus

3. **PathRanker** - Movement path evaluation system
   - Generates all possible movement paths
   - Validates paths (fall tolerance, range, etc.)
   - Ranks paths using utility scoring
   - Different rankers for different unit types (Basic, Infantry, Aerospace, Utility)

4. **FireControl** - Weapon targeting and firing decisions
   - Calculates expected damage, criticals, kills
   - Evaluates heat management
   - Prioritizes targets based on threat/value
   - Creates FiringPlans with weapon selections

5. **FiringPlan** - Complete attack plan for a unit
   - List of WeaponFireInfo objects
   - Includes torso twist and arm flip decisions
   - Calculates total heat, expected damage, expected criticals
   - Probability-based kill chance calculation

6. **EntityState** - Hypothetical unit state tracking
   - Position, facing, heat, movement type
   - Used for "what-if" analysis
   - Enables prediction of future states

7. **Precognition** - Enemy action prediction (runs in separate thread)
   - Maintains parallel game state
   - Pre-calculates enemy movement possibilities
   - Updates during opponent turns to minimize bot thinking time

8. **UnitBehavior** - Behavior state machine
   - ForcedWithdrawal: Crippled units fleeing
   - MoveToDestination: Moving to waypoint/edge
   - MoveToContact: Closing with enemy
   - Engaged: Active combat
   - NoPathToDestination: Stuck/blocked

### 1.2 Utility-Based Decision Making

Princess uses **utility scoring** throughout its decision-making:

```java
// From FireControl.java
protected static final double DAMAGE_UTILITY = 1.0;
protected static final double CRITICAL_UTILITY = 10.0;
protected static final double KILL_UTILITY = 50.0;
protected static final double OVERHEAT_DISUTILITY = 5.0;
protected static final double EJECTED_PILOT_DISUTILITY = 1000.0;
protected static final double CIVILIAN_TARGET_DISUTILITY = 250.0;
protected static final double TARGET_HP_FRACTION_DEALT_UTILITY = -30.0;

private static final double TARGET_POTENTIAL_DAMAGE_UTILITY = 1.0;
static final double COMMANDER_UTILITY = 0.5;
static final double SUB_COMMANDER_UTILITY = 0.25;
static final double STRATEGIC_TARGET_UTILITY = 0.5;
static final double PRIORITY_TARGET_UTILITY = 0.25;
```

**Key Insight:** Each action is scored based on multiple factors, and the highest-scoring action is selected. This allows for nuanced decision-making that balances competing priorities.

### 1.3 Path Ranking System

Princess evaluates movement paths through a sophisticated ranking system:

1. **Path Generation**: Creates all possible movement paths for a unit
2. **Path Validation**: Filters out invalid paths (too risky, out of range, etc.)
3. **Path Ranking**: Scores each valid path based on:
   - Expected damage dealt from that position
   - Expected damage received at that position
   - Distance to objectives/enemies
   - Terrain advantages (cover, elevation)
   - Ally proximity (herd mentality)
   - Fall risk tolerance

4. **Path Selection**: Chooses highest-ranked path

**Key Insight:** Movement and combat are evaluated together - the best movement is the one that sets up the best attack while minimizing risk.

---

## 2. Adapting Princess Concepts to MakaMek

### 2.1 Architectural Alignment

MakaMek's existing architecture aligns well with Princess concepts:

| Princess Component | MakaMek Equivalent | Notes |
|-------------------|-------------------|-------|
| BehaviorSettings | BotDifficulty enum | Should be expanded to BehaviorSettings class |
| PathRanker | MovementEngine | Should implement path ranking logic |
| FireControl | WeaponsEngine | Should implement firing plan evaluation |
| FiringPlan | Internal to WeaponsEngine | Create FiringPlan class |
| EntityState | Can use existing Unit class | May need lightweight wrapper |
| Precognition | Not needed initially | MakaMek is turn-based, less time pressure |
| UnitBehavior | Should be added | Create BehaviorTracker class |

### 2.2 Key Differences

**MegaMek vs MakaMek:**

1. **Language**: Java vs C# (syntax differences, but concepts transfer)
2. **Architecture**: Princess is monolithic, MakaMek uses command pattern
3. **Networking**: Princess is client-side only, MakaMek has client-server separation
4. **Complexity**: MegaMek supports all BattleTech rules, MakaMek is focused subset
5. **Threading**: Princess uses Precognition thread, MakaMek uses async/await
6. **State Management**: Princess directly manipulates game state, MakaMek uses commands

**Implications for MakaMek:**
- Use async/await instead of threading
- Decision engines publish commands instead of directly modifying state
- Leverage existing calculators (ToHitCalculator, PilotingSkillCalculator)
- Start simple, add complexity incrementally
- Focus on core mech combat first (no aerospace, infantry initially)

---

## 3. DecisionEngine Implementation Recommendations

### 3.1 Shared Infrastructure

Before implementing individual engines, create shared infrastructure:

#### 3.1.1 BehaviorSettings Class

```csharp
namespace Sanet.MakaMek.Bots;

/// <summary>
/// Configurable behavior settings for bot personality
/// </summary>
public class BehaviorSettings
{
    // Indices range from 0-10, with 5 being "balanced"

    /// <summary>
    /// How much the bot prioritizes self-preservation (0=reckless, 10=cowardly)
    /// </summary>
    public int SelfPreservationIndex { get; set; } = 5;

    /// <summary>
    /// How much the bot wants to avoid falling (0=don't care, 10=extremely cautious)
    /// </summary>
    public int FallAvoidanceIndex { get; set; } = 5;

    /// <summary>
    /// How aggressively the bot closes with enemies (0=defensive, 10=suicidal)
    /// </summary>
    public int AggressionIndex { get; set; } = 5;

    /// <summary>
    /// How much the bot tries to stay near teammates (0=lone wolf, 10=pack animal)
    /// </summary>
    public int HerdMentalityIndex { get; set; } = 5;

    /// <summary>
    /// How quickly the bot flees when damaged (0=fight to death, 10=flee at first scratch)
    /// </summary>
    public int BraveryIndex { get; set; } = 5;

    /// <summary>
    /// Priority target unit IDs
    /// </summary>
    public HashSet<Guid> PriorityTargets { get; set; } = new();

    /// <summary>
    /// Creates default settings based on difficulty
    /// </summary>
    public static BehaviorSettings FromDifficulty(BotDifficulty difficulty)
    {
        return difficulty switch
        {
            BotDifficulty.Easy => new BehaviorSettings
            {
                SelfPreservationIndex = 7,  // Cautious
                FallAvoidanceIndex = 8,     // Very cautious about falling
                AggressionIndex = 3,        // Defensive
                HerdMentalityIndex = 6,     // Sticks together
                BraveryIndex = 3            // Flees easily
            },
            BotDifficulty.Medium => new BehaviorSettings
            {
                SelfPreservationIndex = 5,  // Balanced
                FallAvoidanceIndex = 5,     // Balanced
                AggressionIndex = 5,        // Balanced
                HerdMentalityIndex = 5,     // Balanced
                BraveryIndex = 5            // Balanced
            },
            BotDifficulty.Hard => new BehaviorSettings
            {
                SelfPreservationIndex = 4,  // Aggressive but smart
                FallAvoidanceIndex = 6,     // Careful about PSRs
                AggressionIndex = 7,        // Aggressive
                HerdMentalityIndex = 4,     // Independent
                BraveryIndex = 7            // Brave
            },
            _ => new BehaviorSettings()
        };
    }
}
```

#### 3.1.2 UnitBehaviorTracker Class

```csharp
namespace Sanet.MakaMek.Bots;

/// <summary>
/// Tracks the current behavior state of each bot-controlled unit
/// </summary>
public class UnitBehaviorTracker
{
    public enum BehaviorType
    {
        /// <summary>Unit is crippled and fleeing</summary>
        ForcedWithdrawal,

        /// <summary>Unit is moving toward enemy contact</summary>
        MoveToContact,

        /// <summary>Unit is actively engaged in combat</summary>
        Engaged,

        /// <summary>Unit is holding position</summary>
        HoldPosition
    }

    private readonly Dictionary<Guid, BehaviorType> _unitBehaviors = new();

    public BehaviorType GetBehavior(Unit unit, ClientGame game, BehaviorSettings settings)
    {
        // Check if we've already determined this unit's behavior
        if (_unitBehaviors.TryGetValue(unit.Id, out var cached))
            return cached;

        // Calculate behavior based on unit state
        var behavior = CalculateBehavior(unit, game, settings);
        _unitBehaviors[unit.Id] = behavior;
        return behavior;
    }

    private BehaviorType CalculateBehavior(Unit unit, ClientGame game, BehaviorSettings settings)
    {
        // Forced withdrawal if crippled
        if (unit.IsCrippled)
            return BehaviorType.ForcedWithdrawal;

        // Check if any enemies are visible
        var enemies = GetVisibleEnemies(unit, game);
        if (!enemies.Any())
            return BehaviorType.MoveToContact;

        // Check if any enemies are in weapon range
        var enemiesInRange = enemies.Where(e => IsInWeaponRange(unit, e, game));
        if (enemiesInRange.Any())
            return BehaviorType.Engaged;

        return BehaviorType.MoveToContact;
    }

    public void Reset()
    {
        _unitBehaviors.Clear();
    }

    private IEnumerable<Unit> GetVisibleEnemies(Unit unit, ClientGame game)
    {
        // TODO: Implement line of sight checking
        return game.Players
            .Where(p => p.Id != unit.PlayerId)
            .SelectMany(p => p.AliveUnits);
    }

    private bool IsInWeaponRange(Unit attacker, Unit target, ClientGame game)
    {
        // TODO: Implement weapon range checking
        var distance = game.BattleMap?.GetDistance(attacker.Position, target.Position) ?? int.MaxValue;
        return distance <= attacker.GetMaxWeaponRange();
    }
}
```

#### 3.1.3 Utility Calculation Helper

```csharp
namespace Sanet.MakaMek.Bots.Utilities;

/// <summary>
/// Utility constants and calculation helpers for bot decision-making
/// </summary>
public static class UtilityConstants
{
    // Weapon firing utilities
    public const double DAMAGE_UTILITY = 1.0;
    public const double CRITICAL_UTILITY = 10.0;
    public const double KILL_UTILITY = 50.0;
    public const double OVERHEAT_DISUTILITY = 5.0;
    public const double SHUTDOWN_DISUTILITY = 100.0;
    public const double AMMO_EXPLOSION_DISUTILITY = 500.0;

    // Target prioritization
    public const double PRIORITY_TARGET_UTILITY = 25.0;
    public const double DAMAGED_TARGET_UTILITY = 10.0;
    public const double IMMOBILE_TARGET_UTILITY = 15.0;

    // Movement utilities
    public const double COVER_UTILITY = 20.0;
    public const double ELEVATION_UTILITY = 10.0;
    public const double FALL_RISK_DISUTILITY = 30.0;
    public const double ALLY_PROXIMITY_UTILITY = 5.0;

    /// <summary>
    /// Calculates expected damage utility
    /// </summary>
    public static double CalculateExpectedDamage(double damage, double hitProbability)
    {
        return damage * hitProbability * DAMAGE_UTILITY;
    }

    /// <summary>
    /// Calculates expected critical hit utility
    /// </summary>
    public static double CalculateExpectedCriticals(double damage, double hitProbability, int targetArmor)
    {
        // Simplified: if damage exceeds armor, chance of critical
        if (damage <= targetArmor) return 0;

        var criticalChance = hitProbability * 0.5; // Rough approximation
        return criticalChance * CRITICAL_UTILITY;
    }

    /// <summary>
    /// Calculates kill probability utility
    /// </summary>
    public static double CalculateKillUtility(double damage, int targetRemainingHP, double hitProbability)
    {
        if (damage < targetRemainingHP) return 0;

        return hitProbability * KILL_UTILITY;
    }
}
```


---

### 3.2 DeploymentEngine Implementation

**Goal:** Deploy units in tactically advantageous positions

**Princess Approach:**
- Deploys units spread out to avoid clustering
- Considers terrain (cover, elevation)
- Groups units by lance/role
- Avoids deployment zones with enemy fire coverage

**MakaMek Implementation:**

```csharp
public class DeploymentEngine : IBotDecisionEngine
{
    private readonly ClientGame _clientGame;
    private readonly IPlayer _player;
    private readonly BehaviorSettings _settings;

    public async Task MakeDecision()
    {
        try
        {
            // 1. Find undeployed units
            var undeployedUnits = _player.Units
                .Where(u => !u.IsDeployed)
                .OrderByDescending(u => u.Tonnage) // Deploy heavier units first
                .ToList();

            if (!undeployedUnits.Any())
            {
                await SkipTurn();
                return;
            }

            var unit = undeployedUnits.First();

            // 2. Get valid deployment hexes
            var validHexes = GetValidDeploymentHexes();

            // 3. Score each hex
            var scoredHexes = validHexes
                .Select(hex => new
                {
                    Hex = hex,
                    Score = ScoreDeploymentHex(hex, unit)
                })
                .OrderByDescending(x => x.Score)
                .ToList();

            // 4. Select best hex (with some randomness for Easy difficulty)
            var selectedHex = SelectDeploymentHex(scoredHexes);

            // 5. Select facing toward enemy
            var facing = SelectDeploymentFacing(selectedHex.Hex);

            // 6. Deploy unit
            var command = new DeployUnitCommand
            {
                GameOriginId = _clientGame.Id,
                PlayerId = _player.Id,
                UnitId = unit.Id,
                Position = selectedHex.Hex,
                Direction = facing
            };

            _clientGame.DeployUnit(command);
        }
        catch (Exception ex)
        {
            // Log error and skip turn
            Console.WriteLine($"Deployment error: {ex.Message}");
            await SkipTurn();
        }
    }

    private double ScoreDeploymentHex(HexCoordinate hex, Unit unit)
    {
        double score = 0;

        // Prefer hexes with cover
        var terrain = _clientGame.BattleMap?.GetHex(hex);
        if (terrain?.HasCover == true)
            score += 20;

        // Prefer elevation
        score += (terrain?.Elevation ?? 0) * 5;

        // Avoid clustering with already-deployed units
        var nearbyAllies = _player.Units
            .Where(u => u.IsDeployed)
            .Count(u => _clientGame.BattleMap?.GetDistance(hex, u.Position) <= 2);
        score -= nearbyAllies * 10;

        // Prefer positions closer to center (avoid edges)
        var distanceFromCenter = GetDistanceFromMapCenter(hex);
        score -= distanceFromCenter * 2;

        return score;
    }

    private HexCoordinate SelectDeploymentHex(List<(HexCoordinate Hex, double Score)> scoredHexes)
    {
        // Easy: More randomness
        // Medium: Some randomness
        // Hard: Best choice

        var topChoices = _settings.Difficulty switch
        {
            BotDifficulty.Easy => scoredHexes.Take(10).ToList(),
            BotDifficulty.Medium => scoredHexes.Take(5).ToList(),
            BotDifficulty.Hard => scoredHexes.Take(1).ToList(),
            _ => scoredHexes.Take(5).ToList()
        };

        var random = new Random();
        return topChoices[random.Next(topChoices.Count)].Hex;
    }
}
```

**Key Concepts from Princess:**
1. **Terrain evaluation**: Score hexes based on cover, elevation
2. **Spacing**: Avoid clustering units
3. **Difficulty-based randomness**: Easy bots make more random choices
4. **Tonnage-based priority**: Deploy heavy units first


---

### 3.3 MovementEngine Implementation

**Goal:** Move units to optimal positions for combat

**Princess Approach:**
- Generates ALL possible movement paths
- Validates paths (fall risk, heat, etc.)
- Scores paths based on:
  - Expected damage from that position
  - Expected damage received
  - Distance to enemies/objectives
  - Terrain advantages
  - Ally proximity
- Selects highest-scoring path

**MakaMek Implementation Strategy:**

**Phase 1: Simple Random Movement (Easy difficulty baseline)**

```csharp
public class MovementEngine : IBotDecisionEngine
{
    private readonly ClientGame _clientGame;
    private readonly IPlayer _player;
    private readonly BotDifficulty _difficulty;

    public async Task MakeDecision()
    {
        try
        {
            // 1. Find unmoved units
            var unmovedUnits = _player.AliveUnits
                .Where(u => u.IsDeployed && u.MovementTypeUsed == null)
                .ToList();

            if (!unmovedUnits.Any())
            {
                await SkipTurn();
                return;
            }

            var unit = unmovedUnits.First();

            // 2. Handle prone units
            if (unit.IsProne)
            {
                await AttemptStandUp(unit);
                return;
            }

            // 3. Select movement type
            var movementType = SelectMovementType(unit);

            // 4. Generate and select path
            var path = GenerateMovementPath(unit, movementType);

            if (path == null)
            {
                await StandStill(unit);
                return;
            }

            // 5. Execute movement
            await ExecuteMovement(unit, path, movementType);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Movement error: {ex.Message}");
            await SkipTurn();
        }
    }

    private MovementType SelectMovementType(Unit unit)
    {
        // Easy: Prefer walk
        // Medium: Walk or run based on situation
        // Hard: Optimal choice including jump

        if (_difficulty == BotDifficulty.Easy)
        {
            return unit.CanWalk ? MovementType.Walk : MovementType.StandStill;
        }

        // Check if we should run (enemies far away)
        var nearestEnemy = FindNearestEnemy(unit);
        if (nearestEnemy != null)
        {
            var distance = _clientGame.BattleMap?.GetDistance(unit.Position, nearestEnemy.Position) ?? 0;

            // If enemy is far, run to close distance
            if (distance > 10 && unit.CanRun)
                return MovementType.Run;

            // If enemy is close, walk to maintain accuracy
            if (distance <= 6 && unit.CanWalk)
                return MovementType.Walk;
        }

        return unit.CanWalk ? MovementType.Walk : MovementType.StandStill;
    }
}
```

**Phase 2: Path Ranking System (Medium/Hard difficulty)**

```csharp
public class MovementEngine : IBotDecisionEngine
{
    private readonly UnitBehaviorTracker _behaviorTracker;
    private readonly BehaviorSettings _settings;

    private List<RankedMovementPath> GenerateAndRankPaths(Unit unit, UnitBehaviorTracker.BehaviorType behavior)
    {
        // Generate possible paths
        var paths = GenerateMovementPaths(unit);

        // Validate and rank
        return paths
            .Where(p => IsPathValid(p, unit))
            .Select(p => new RankedMovementPath
            {
                Path = p,
                Score = ScorePath(p, unit, behavior)
            })
            .OrderByDescending(p => p.Score)
            .Take(20) // Keep top 20 to avoid excessive computation
            .ToList();
    }

    private double ScorePath(MovementPath path, Unit unit, UnitBehaviorTracker.BehaviorType behavior)
    {
        double score = 0;

        // Base scoring on behavior
        score += behavior switch
        {
            UnitBehaviorTracker.BehaviorType.ForcedWithdrawal => ScoreFleePath(path, unit),
            UnitBehaviorTracker.BehaviorType.MoveToContact => ScoreAdvancePath(path, unit),
            UnitBehaviorTracker.BehaviorType.Engaged => ScoreCombatPath(path, unit),
            _ => 0
        };

        // Adjust for terrain
        score += ScoreTerrain(path.FinalPosition);

        // Adjust for fall risk
        var fallRisk = CalculateFallRisk(path, unit);
        score -= fallRisk * UtilityConstants.FALL_RISK_DISUTILITY * _settings.FallAvoidanceIndex / 5.0;

        // Adjust for heat
        var heatGenerated = CalculateHeatGenerated(path);
        score -= heatGenerated * 2;

        // Adjust for ally proximity (herd mentality)
        var allyProximity = CalculateAllyProximity(path.FinalPosition);
        score += allyProximity * UtilityConstants.ALLY_PROXIMITY_UTILITY * _settings.HerdMentalityIndex / 5.0;

        return score;
    }

    private double ScoreCombatPath(MovementPath path, Unit unit)
    {
        double score = 0;

        // Find best target from this position
        var enemies = GetVisibleEnemies(unit, path.FinalPosition);
        if (!enemies.Any()) return -100; // Bad position if no targets

        var bestTarget = enemies
            .Select(e => new
            {
                Enemy = e,
                ExpectedDamage = EstimateDamageToTarget(unit, e, path.FinalPosition),
                ThreatLevel = EstimateThreatLevel(e),
                IsPriority = _settings.PriorityTargets.Contains(e.Id)
            })
            .OrderByDescending(x => x.ExpectedDamage + x.ThreatLevel + (x.IsPriority ? 50 : 0))
            .FirstOrDefault();

        if (bestTarget != null)
        {
            // Reward positions that allow good damage
            score += bestTarget.ExpectedDamage;

            // Bonus for priority targets
            if (bestTarget.IsPriority)
                score += UtilityConstants.PRIORITY_TARGET_UTILITY;

            // Consider range - prefer optimal range for weapons
            var distance = GetDistance(path.FinalPosition, bestTarget.Enemy.Position);
            var optimalRange = GetOptimalWeaponRange(unit);
            score -= Math.Abs(distance - optimalRange) * 5;
        }

        // Penalize positions where we'll take damage
        var expectedDamageReceived = EstimateDamageReceived(unit, path.FinalPosition);
        score -= expectedDamageReceived * _settings.SelfPreservationIndex / 5.0;

        return score;
    }

    private double ScoreAdvancePath(MovementPath path, Unit unit)
    {
        // Reward paths that close distance to nearest enemy
        var nearestEnemy = FindNearestEnemy(unit);
        if (nearestEnemy == null) return 0;

        var currentDistance = GetDistance(unit.Position, nearestEnemy.Position);
        var newDistance = GetDistance(path.FinalPosition, nearestEnemy.Position);

        // Reward closing distance
        return (currentDistance - newDistance) * 10;
    }

    private double ScoreFleePath(MovementPath path, Unit unit)
    {
        // Reward paths that increase distance from enemies
        var nearestEnemy = FindNearestEnemy(unit);
        if (nearestEnemy == null) return 0;

        var currentDistance = GetDistance(unit.Position, nearestEnemy.Position);
        var newDistance = GetDistance(path.FinalPosition, nearestEnemy.Position);

        // Reward increasing distance
        return (newDistance - currentDistance) * 10;
    }
}
```

**Key Concepts from Princess:**
1. **Path generation**: Create multiple possible paths
2. **Path validation**: Filter out risky/invalid paths
3. **Utility-based scoring**: Evaluate paths on multiple criteria
4. **Behavior-driven decisions**: Different scoring for different situations
5. **Fall risk management**: Heavily penalize risky movements based on settings
6. **Heat awareness**: Consider heat buildup in movement decisions

---

### 3.4 WeaponsEngine Implementation

**Goal:** Select optimal targets and weapons for maximum effectiveness

**Princess Approach:**
- Creates FiringPlan for each possible target
- Evaluates each plan using utility scoring:
  - Expected damage (damage × hit probability)
  - Expected criticals
  - Kill probability
  - Heat management
  - Friendly fire avoidance
- Selects highest-utility firing plan
- Includes torso twist and arm flip decisions

**MakaMek Implementation:**

**Phase 1: Simple Target Selection (Easy difficulty)**

```csharp
public class WeaponsEngine : IBotDecisionEngine
{
    private readonly ClientGame _clientGame;
    private readonly IPlayer _player;
    private readonly BotDifficulty _difficulty;

    public async Task MakeDecision()
    {
        try
        {
            // 1. Find units that haven't attacked
            var unattackedUnits = _player.AliveUnits
                .Where(u => !u.HasDeclaredWeaponAttack && u.CanFireWeapons)
                .ToList();

            if (!unattackedUnits.Any())
            {
                await SkipTurn();
                return;
            }

            var attacker = unattackedUnits.First();

            // 2. Find potential targets
            var targets = FindPotentialTargets(attacker);

            if (!targets.Any())
            {
                await SkipTurn();
                return;
            }

            // 3. Select target (random for Easy, best for Hard)
            var target = SelectTarget(targets, attacker);

            // 4. Select weapons to fire
            var weaponsToFire = SelectWeapons(attacker, target);

            if (!weaponsToFire.Any())
            {
                await SkipTurn();
                return;
            }

            // 5. Declare attack
            await DeclareAttack(attacker, target, weaponsToFire);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Weapons error: {ex.Message}");
            await SkipTurn();
        }
    }

    private List<Unit> FindPotentialTargets(Unit attacker)
    {
        return _clientGame.Players
            .Where(p => p.Id != attacker.PlayerId)
            .SelectMany(p => p.AliveUnits)
            .Where(target => IsInWeaponRange(attacker, target))
            .ToList();
    }

    private List<Weapon> SelectWeapons(Unit attacker, Unit target)
    {
        // Simple approach: fire all weapons in range
        var distance = _clientGame.BattleMap?.GetDistance(attacker.Position, target.Position) ?? int.MaxValue;

        return attacker.Weapons
            .Where(w => w.CanFire && w.MaxRange >= distance && w.MinRange <= distance)
            .ToList();
    }
}
```

**Phase 2: FiringPlan System (Medium/Hard difficulty)**

```csharp
public class WeaponsEngine : IBotDecisionEngine
{
    private readonly BehaviorSettings _settings;

    public async Task MakeDecision()
    {
        try
        {
            var unattackedUnits = _player.AliveUnits
                .Where(u => !u.HasDeclaredWeaponAttack && u.CanFireWeapons)
                .ToList();

            if (!unattackedUnits.Any())
            {
                await SkipTurn();
                return;
            }

            var attacker = unattackedUnits.First();

            // Generate firing plans for all potential targets
            var firingPlans = GenerateFiringPlans(attacker);

            if (!firingPlans.Any())
            {
                await SkipTurn();
                return;
            }

            // Select best plan
            var bestPlan = SelectFiringPlan(firingPlans);

            // Execute plan
            await ExecuteFiringPlan(attacker, bestPlan);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Weapons error: {ex.Message}");
            await SkipTurn();
        }
    }

    private List<FiringPlan> GenerateFiringPlans(Unit attacker)
    {
        var targets = FindPotentialTargets(attacker);
        var plans = new List<FiringPlan>();

        foreach (var target in targets)
        {
            var plan = CreateFiringPlan(attacker, target);
            if (plan != null && plan.Weapons.Any())
            {
                plans.Add(plan);
            }
        }

        return plans;
    }

    private FiringPlan CreateFiringPlan(Unit attacker, Unit target)
    {
        var plan = new FiringPlan
        {
            Target = target,
            Weapons = new List<WeaponSelection>()
        };

        var distance = _clientGame.BattleMap?.GetDistance(attacker.Position, target.Position) ?? int.MaxValue;

        // Evaluate each weapon
        foreach (var weapon in attacker.Weapons.Where(w => w.CanFire))
        {
            if (weapon.MinRange > distance || weapon.MaxRange < distance)
                continue;

            // Calculate to-hit
            var toHit = _clientGame.ToHitCalculator.CalculateToHit(attacker, target, weapon, distance);
            var hitProbability = CalculateHitProbability(toHit.ModifiedGunnerySkill);

            // Calculate expected damage
            var expectedDamage = weapon.Damage * hitProbability;

            // Calculate heat cost
            var heatCost = weapon.Heat;

            plan.Weapons.Add(new WeaponSelection
            {
                Weapon = weapon,
                HitProbability = hitProbability,
                ExpectedDamage = expectedDamage,
                HeatCost = heatCost
            });
        }

        // Calculate plan utility
        plan.Utility = CalculatePlanUtility(plan, attacker);

        return plan;
    }

    private double CalculatePlanUtility(FiringPlan plan, Unit attacker)
    {
        double utility = 0;

        // Expected damage utility
        var totalExpectedDamage = plan.Weapons.Sum(w => w.ExpectedDamage);
        utility += totalExpectedDamage * UtilityConstants.DAMAGE_UTILITY;

        // Kill utility (if damage likely to destroy target)
        var targetHP = EstimateTargetHP(plan.Target);
        if (totalExpectedDamage >= targetHP * 0.8)
        {
            utility += UtilityConstants.KILL_UTILITY;
        }

        // Priority target bonus
        if (_settings.PriorityTargets.Contains(plan.Target.Id))
        {
            utility += UtilityConstants.PRIORITY_TARGET_UTILITY;
        }

        // Damaged target bonus (finish off wounded enemies)
        if (plan.Target.IsDamaged)
        {
            utility += UtilityConstants.DAMAGED_TARGET_UTILITY;
        }

        // Immobile target bonus (easier to hit)
        if (plan.Target.IsImmobile || plan.Target.IsProne)
        {
            utility += UtilityConstants.IMMOBILE_TARGET_UTILITY;
        }

        // Heat management
        var totalHeat = plan.Weapons.Sum(w => w.HeatCost);
        var currentHeat = attacker.GetHeatData().CurrentHeat;
        var projectedHeat = currentHeat + totalHeat;

        // Penalize overheat
        if (projectedHeat > attacker.GetHeatData().HeatSinkCapacity)
        {
            var overheat = projectedHeat - attacker.GetHeatData().HeatSinkCapacity;
            utility -= overheat * UtilityConstants.OVERHEAT_DISUTILITY;
        }

        // Heavily penalize shutdown risk
        if (projectedHeat >= 30)
        {
            utility -= UtilityConstants.SHUTDOWN_DISUTILITY;
        }

        // Penalize ammo explosion risk
        if (projectedHeat >= 19 && attacker.HasAmmo)
        {
            utility -= UtilityConstants.AMMO_EXPLOSION_DISUTILITY * (projectedHeat - 18) / 10.0;
        }

        return utility;
    }

    private FiringPlan SelectFiringPlan(List<FiringPlan> plans)
    {
        // Sort by utility
        var sortedPlans = plans.OrderByDescending(p => p.Utility).ToList();

        // Difficulty-based selection
        return _difficulty switch
        {
            BotDifficulty.Easy => sortedPlans[new Random().Next(Math.Min(3, sortedPlans.Count))],
            BotDifficulty.Medium => sortedPlans[new Random().Next(Math.Min(2, sortedPlans.Count))],
            BotDifficulty.Hard => sortedPlans.First(),
            _ => sortedPlans.First()
        };
    }
}
```

**Key Concepts from Princess:**
1. **FiringPlan abstraction**: Encapsulates complete attack decision
2. **Utility-based weapon selection**: Balance damage vs heat
3. **Target prioritization**: Focus fire on valuable/vulnerable targets
4. **Heat management**: Avoid shutdown and ammo explosions
5. **Probability calculations**: Use hit probability for expected damage
6. **Kill focus**: Bonus utility for finishing off damaged enemies

---

### 3.5 EndPhaseEngine Implementation

**Goal:** Manage heat, shutdown/startup decisions, and end turn

**Princess Approach:**
- Evaluates shutdown risk vs combat effectiveness
- Always attempts restart for shutdown units
- Considers strategic shutdown to avoid ammo explosion
- Manages heat buildup across turns

**MakaMek Implementation:**

```csharp
public class EndPhaseEngine : IBotDecisionEngine
{
    private readonly ClientGame _clientGame;
    private readonly IPlayer _player;
    private readonly BehaviorSettings _settings;

    public async Task MakeDecision()
    {
        try
        {
            // 1. Handle shutdown units (attempt restart)
            var shutdownUnits = _player.AliveUnits.Where(u => u.IsShutdown).ToList();
            foreach (var unit in shutdownUnits)
            {
                if (ShouldAttemptRestart(unit))
                {
                    await AttemptRestart(unit);
                    return; // One action per turn
                }
            }

            // 2. Handle overheated units (consider shutdown)
            var overheatedUnits = _player.AliveUnits
                .Where(u => !u.IsShutdown && ShouldShutdown(unit))
                .ToList();

            if (overheatedUnits.Any())
            {
                var unit = overheatedUnits.First();
                await ShutdownUnit(unit);
                return;
            }

            // 3. End turn
            await EndTurn();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"End phase error: {ex.Message}");
            await EndTurn();
        }
    }

    private bool ShouldAttemptRestart(Unit unit)
    {
        // Always attempt restart unless in extreme danger
        var heatData = unit.GetHeatData();

        // Don't restart if ammo explosion is imminent
        if (heatData.CurrentHeat >= 28 && unit.HasAmmo)
            return false;

        return true;
    }

    private bool ShouldShutdown(Unit unit)
    {
        var heatData = unit.GetHeatData();
        var currentHeat = heatData.CurrentHeat;

        // Mandatory shutdown at 30+
        if (currentHeat >= 30)
            return true;

        // Strategic shutdown to avoid ammo explosion
        if (currentHeat >= 23 && unit.HasAmmo)
        {
            // Shutdown if we have ammo and heat is dangerous
            return true;
        }

        // Shutdown if heat is very high and we're not in immediate combat
        if (currentHeat >= 25)
        {
            var nearestEnemy = FindNearestEnemy(unit);
            if (nearestEnemy == null || GetDistance(unit.Position, nearestEnemy.Position) > 10)
            {
                return true; // Safe to shutdown, no immediate threat
            }
        }

        return false;
    }
}
```

**Key Concepts from Princess:**
1. **Always restart**: Shutdown units should attempt restart
2. **Strategic shutdown**: Prevent ammo explosions
3. **Heat threshold management**: Different thresholds for different situations
4. **Tactical awareness**: Consider combat situation when deciding to shutdown

---

## 4. Implementation Priorities and Roadmap

### 4.1 Phase 1: Foundation (Week 1-2)

**Goal:** Get basic bot functionality working

1. **Create shared infrastructure:**
   - BehaviorSettings class
   - UnitBehaviorTracker class
   - UtilityConstants helper
   - FiringPlan class

2. **Implement Easy difficulty for all engines:**
   - DeploymentEngine: Random valid deployment
   - MovementEngine: Simple walk toward enemy
   - WeaponsEngine: Fire all weapons at nearest target
   - EndPhaseEngine: Basic heat management

3. **Integration:**
   - Update Bot class to use BehaviorSettings
   - Update BotManager to create BehaviorSettings from difficulty
   - Add necessary helper methods to Unit class (GetMaxWeaponRange, etc.)

**Success Criteria:**
- Bot can complete a full game without errors
- Bot makes legal moves in all phases
- Bot doesn't crash or hang the game

### 4.2 Phase 2: Intelligence (Week 3-4)

**Goal:** Add tactical decision-making

1. **Enhance MovementEngine:**
   - Implement path generation
   - Add path scoring system
   - Implement behavior-based movement (Engaged, MoveToContact, ForcedWithdrawal)

2. **Enhance WeaponsEngine:**
   - Implement FiringPlan system
   - Add utility-based target selection
   - Implement heat management in weapon selection

3. **Add difficulty scaling:**
   - Medium difficulty uses path ranking
   - Hard difficulty uses optimal decisions

**Success Criteria:**
- Bot makes tactically sound decisions
- Bot manages heat effectively
- Bot shows different behavior at different difficulties

### 4.3 Phase 3: Polish (Week 5-6)

**Goal:** Refine bot behavior and add advanced features

1. **Advanced features:**
   - Torso twist decisions
   - Jump jet usage
   - Terrain exploitation (cover, elevation)
   - Focus fire on damaged enemies

2. **Behavior tuning:**
   - Adjust utility constants based on testing
   - Add randomness to prevent predictability
   - Implement thinking delays

3. **Testing and optimization:**
   - Bot vs Bot games
   - Performance profiling
   - Edge case handling

**Success Criteria:**
- Bot provides challenging gameplay
- Bot behavior feels natural
- No performance issues

---

## 5. Testing Strategies

### 5.1 Unit Testing

**Test each DecisionEngine in isolation:**

```csharp
[Fact]
public void DeploymentEngine_ShouldDeployAllUnits()
{
    // Arrange
    var game = CreateTestGame();
    var player = CreateTestPlayer(unitCount: 4);
    var engine = new DeploymentEngine(game, player, BotDifficulty.Easy);

    // Act
    for (int i = 0; i < 4; i++)
    {
        await engine.MakeDecision();
    }

    // Assert
    player.Units.All(u => u.IsDeployed).ShouldBeTrue();
}

[Fact]
public void WeaponsEngine_ShouldNotOverheat()
{
    // Arrange
    var game = CreateTestGame();
    var unit = CreateTestUnit(currentHeat: 20);
    var engine = new WeaponsEngine(game, player, BotDifficulty.Hard);

    // Act
    await engine.MakeDecision();

    // Assert
    unit.GetHeatData().CurrentHeat.ShouldBeLessThan(30);
}
```

### 5.2 Integration Testing

**Test full game scenarios:**

```csharp
[Fact]
public async Task Bot_ShouldCompleteFullGame()
{
    // Arrange
    var game = CreateTestGame();
    var botPlayer = CreateBotPlayer();
    var humanPlayer = CreateHumanPlayer();

    // Act
    var result = await PlayGameToCompletion(game);

    // Assert
    result.ShouldNotBeNull();
    result.Winner.ShouldNotBeNull();
}
```

### 5.3 Manual Testing Checklist

- [ ] Bot deploys all units
- [ ] Bot moves all units each turn
- [ ] Bot attacks when targets available
- [ ] Bot manages heat effectively
- [ ] Bot doesn't crash on edge cases
- [ ] Bot completes games without hanging
- [ ] Easy bot is beatable
- [ ] Hard bot is challenging
- [ ] Bot behavior varies between difficulties

---

## 6. Key Differences and Adaptations

### 6.1 What We Can't Port from Princess

1. **Precognition Thread**: MegaMek uses threading for performance; MakaMek uses async/await
2. **Complex Path Enumeration**: Princess generates thousands of paths; start simpler in MakaMek
3. **Aerospace/Infantry Logic**: Focus on mech combat first
4. **Physical Attacks**: Not implemented in MakaMek yet
5. **Advanced Tactics**: Flanking, focus fire coordination - add later

### 6.2 What We Should Adapt

1. **Utility-Based Scoring**: Core concept that works well
2. **BehaviorSettings**: Configurable personality is excellent
3. **FiringPlan Pattern**: Clean abstraction for weapon decisions
4. **Path Ranking**: Evaluate multiple options, pick best
5. **Heat Management**: Critical for BattleTech gameplay
6. **Target Prioritization**: Focus on valuable/vulnerable targets

### 6.3 MakaMek-Specific Advantages

1. **Existing Calculators**: ToHitCalculator, PilotingSkillCalculator already implemented
2. **Command Pattern**: Clean separation of decision and execution
3. **Async/Await**: Modern C# async patterns
4. **Strong Typing**: C# type system helps prevent errors
5. **LINQ**: Powerful query capabilities for filtering/sorting

---

## 7. Conclusion

The MegaMek Princess bot provides an excellent blueprint for implementing intelligent bot players in MakaMek. The key insights are:

1. **Utility-Based Decision Making**: Score actions based on multiple factors, select highest-scoring option
2. **Configurable Behavior**: Use settings to create different bot personalities and difficulties
3. **Incremental Implementation**: Start simple (random decisions), add intelligence gradually
4. **Heat Management**: Critical for BattleTech, must be central to decision-making
5. **Path Ranking**: Evaluate movement and combat together for optimal positioning

**Recommended Implementation Order:**

1. **Week 1-2**: Shared infrastructure + Easy difficulty (random but legal decisions)
2. **Week 3-4**: Path ranking + FiringPlan system (Medium/Hard difficulty)
3. **Week 5-6**: Polish, tuning, advanced features

**Success Metrics:**

- Bot completes games without errors
- Bot makes tactically sound decisions
- Bot provides appropriate challenge at each difficulty level
- Bot behavior feels natural and varied

By following Princess's architectural patterns while adapting to MakaMek's specific architecture, we can create engaging, challenging bot opponents that enhance the single-player experience.

---

**Next Steps:**

1. Review this document with the team
2. Create implementation tasks in the roadmap
3. Begin Phase 1 implementation (shared infrastructure)
4. Iterate based on testing and feedback
