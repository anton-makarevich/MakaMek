# Bot torso rotation in tactical evaluation

## Scope

This document analyzes the current bot decision-making workflow for:

- Movement position evaluation (`TacticalEvaluator`, `MovementEngine`)
- Weapon targeting and attack declaration (`WeaponsEngine`)

…and proposes enhancements so bots correctly account for **Mech torso rotation** (torso twist) when:

- Scoring offensive potential of candidate movement positions
- Deciding whether to rotate torso during `WeaponsAttack` phase

## Key references

- `src/MakaMek.Bots/Models/DecisionEngines/TacticalEvaluator.cs`
- `src/MakaMek.Bots/Models/DecisionEngines/MovementEngine.cs`
- `src/MakaMek.Bots/Models/DecisionEngines/WeaponsEngine.cs`
- `src/MakaMek.Core/Models/Units/Mechs/Mech.cs`
- `src/MakaMek.Presentation/UiStates/WeaponsAttackState.cs`
- `src/MakaMek.Core/Models/Game/Phases/WeaponsAttackPhase.cs`

Additional important mechanics involved:

- `src/MakaMek.Core/Data/Game/Commands/Client/WeaponConfigurationCommand.cs`
- `src/MakaMek.Core/Models/Game/BaseGame.cs` (`OnWeaponConfiguration`)
- `src/MakaMek.Core/Models/Map/HexCoordinatesExtensions.cs` (`IsInWeaponFiringArc`)

## Current implementation

### 1) Movement decision flow

**Entry point:** `MovementEngine.MakeDecision`.

- Enumerates reachable paths for each movement type (walk/run/jump)
- Evaluates each reachable `MovementPath` with `_evaluator.EvaluatePath(unit, path, enemyUnits)`
- Picks the "best" path by sorting:
  - **DefensiveIndex** ascending
  - **OffensiveIndex** descending
  - EnemiesInRearArc ascending
  - HexesTraveled descending

**OffensiveIndex** is entirely produced by `TacticalEvaluator.EvaluatePath`.

### 2) How offensive score is currently calculated

**Entry point:** `TacticalEvaluator.EvaluatePath`.

- `DefensiveIndex = CalculateDefensiveIndex(path, enemyUnits)`
- `OffensiveIndex = Sum(EvaluateTargets(attacker, attackerPath, enemyUnits).Score)`

`EvaluateTargets`:

- Collects all available weapons on the attacker.
- For each potential target:
  - Builds a `targetPath` from the target's `MovementTaken` (or standing still).
  - Computes `viableWeapons = EvaluateWeaponsForTarget(attacker, attackerPath, targetPath, weapons)`.
  - If any viable weapons exist, it computes:
    - `targetArc` (rear/side/front bonus) based on **target-facing vs attacker position**
    - `targetScore = Sum(hitProbability * weapon.Damage) * arcBonus`

`EvaluateWeaponsForTarget`:

- Checks line-of-sight.
- Checks range.
- Checks firing arc via:
  - `attackerPath.Destination.Coordinates.IsInWeaponFiringArc(targetPath.Destination.Coordinates, weapon, attackerPath.Destination.Facing)`
- Calculates hit probability through `CalculateHitProbability`, which builds a hypothetical `AttackScenario.FromHypothetical` and calls `ToHitCalculator`.

### 3) Weapons phase decision flow (bot)

**Entry point:** `WeaponsEngine.MakeDecision`.

- Chooses a single attacker: first alive unit with `HasDeclaredWeaponAttack == false` and `CanFireWeapons == true`.
- Builds list of enemies.
- Calls `_tacticalEvaluator.EvaluateTargets(attacker, attackerPath, enemies)`.
- Selects the best target by `TargetScore.Score`.
- Picks weapons for that target based on:
  - hit probability
  - damage
  - heat budget
  - ammo conservation heuristic
- Sends `WeaponAttackDeclarationCommand`.

**Limitation:** `WeaponsEngine` never sends a `WeaponConfigurationCommand` for torso rotation.

### 4) How torso rotation works for humans and in backend

#### Core model

In `Mech.cs`:

- `PossibleTorsoRotation` is the max number of hex-side steps allowed.
- `CanRotateTorso => PossibleTorsoRotation > 0 && !HasUsedTorsoTwist`.
- `HasUsedTorsoTwist` is true if any `Torso.Facing != Position.Facing`.
- `RotateTorso(HexDirection newFacing)` sets `Torso.Rotate(newFacing)` if within `PossibleTorsoRotation`.
- `Mech.Facing` is overridden: `TorsoDirection ?? Position?.Facing`.
- Torso rotation resets:
  - When `Mech.Position` changes (movement), all torsos call `ResetRotation()`.
  - On `Mech.ResetTurnState()`, all torsos call `ResetRotation()`.

#### UI flow

`WeaponsAttackState` shows the expected "human" workflow:

- In action selection step, a mech can choose **Turn Torso**.
- It computes `_availableDirections` by scanning all 6 directions and filtering by `steps <= mech.PossibleTorsoRotation`.
- On selection, it sends `WeaponConfigurationCommand`:
  - `Configuration.Type = WeaponConfigurationType.TorsoRotation`
  - `Configuration.Value = (int)direction`
- After rotation, the UI continues (target selection, weapon selection, declare attack).

#### Server phase handling

`WeaponsAttackPhase.HandleCommand`:

- `WeaponConfigurationCommand` is processed by `Game.OnWeaponConfiguration(...)` and re-broadcast.
- `WeaponAttackDeclarationCommand` is handled as the unit action.

`BaseGame.OnWeaponConfiguration` applies torso rotation:

- For `WeaponConfigurationType.TorsoRotation`, it calls `mech.RotateTorso((HexDirection)Value)`.

## Current limitations / why bots mis-evaluate

### A) Offensive evaluation uses the *leg facing*, not a "best possible torso facing"

In `TacticalEvaluator.EvaluateWeaponsForTarget` the arc check passes `attackerPath.Destination.Facing` to `IsInWeaponFiringArc`.

- `HexPosition.Facing` is the unit's base facing (leg facing/"forward facing")
- Torso twist is a separate override on the torso parts (`Torso.Rotate`) and is exposed through `Mech.Facing`

As a result, when a target is slightly outside the front/side arcs for the leg-facing, the bot assigns **0 viable weapons**, even though a torso rotation would bring the target into arc.

### B) The bot never actually torso-twists during weapons phase

Even if a torso twist would unlock significant damage, `WeaponsEngine` goes straight to `WeaponAttackDeclarationCommand`.

### C) Secondary-target handling is also tied to attacker facing

`AttackScenario` includes `AttackerFacing`, which feeds `SecondaryTargetModifier` calculations in `ToHitCalculator`.

Currently `TacticalEvaluator.CalculateHitProbability` passes `attackerFacing: attackerPath.Destination.Facing`, which again ignores torso twist.

(While the current bot uses only one target, this becomes more important once the bot starts splitting fire.)

## Proposed solution overview

The goal is to treat torso rotation as a **choice variable** in the bot's evaluation and decision pipeline:

- During movement evaluation, offensive potential should be computed as:
  - `max_over_possible_torso_facings( expected_damage(attacker_position, torso_facing) )`
- During weapons declaration, the bot should:
  - choose torso facing + target + weapons as a joint optimization
  - issue a `WeaponConfigurationCommand` when beneficial
  - then issue `WeaponAttackDeclarationCommand` (in a subsequent execution)

## Proposed design: torso-aware evaluation

### 1) Computing possible torso configurations

For a given attacker unit, determine available torso rotation options using the existing game API:

```csharp
var torsoConfigs = attacker.GetWeaponsConfigurationOptions(atackerPath.Destination)
    .Where(c => c.Type == WeaponConfigurationType.TorsoRotation)
    .ToList();
```

Always include the "no rotation" option representing the current leg facing:

```csharp
var noRotationConfig = new WeaponConfiguration 
{ 
    Type = WeaponConfigurationType.None,
    Value = (int)attackerPath.Destination.Facing
};

var allConfigs = new[] { noRotationConfig }.Concat(torsoConfigs).ToList();
```

### 2) Torso-aware weapon evaluation (TacticalEvaluator)

**Changes to `EvaluateWeaponsForTarget`:**

1. **Enumerate all configurations** using the approach above
2. **Nested loop structure:**
   ```csharp
   foreach (var config in allConfigs)
   {
       var facing = (HexDirection)config.Value;
       
       foreach (var weapon in weapons)
       {
           // Check configuration applicability
           if (!weapon.FirstMountPart.IsWeaponConfigurationApplicable(config.Type, attackerPath.Destination))
               continue;
           
           // Check arc using resolved facing
           if (!attackerPath.Destination.Coordinates.IsInWeaponFiringArc(
               targetPath.Destination.Coordinates, weapon, facing))  //<-- "facing" is variable
               continue;
           
           // Rest of the existing code
           
           // Add to viable weapons with configuration tag
           viableWeapons.Add(new WeaponEvaluationData
           {
               Weapon = weapon,
               HitProbability = hitProb,
               Configuration = config  // NEW: tag weapon with its config
           });
       }
   }
   ```

3. **Extend `WeaponEvaluationData`:**
   ```csharp
   public class WeaponEvaluationData
   {
       public IWeapon Weapon { get; set; }
       public double HitProbability { get; set; }
       public WeaponConfiguration Configuration { get; set; }  // NEW
   }
   ```

**Result:** The viable weapons list contains entries for all weapon-configuration pairs that pass all checks. Each weapon is tagged with the `WeaponConfiguration` that enables it.

### 3) Configuration-aware scoring (TacticalEvaluator)

**Changes to `EvaluatePath`/`EvaluateTargets`:**

When computing offensive score for each target:

```csharp
// Group viable weapons by their required configuration
var configGroups = viableWeapons.GroupBy(w => w.Configuration);

// Calculate score for each configuration
var configScores = configGroups.Select(group => 
{
    var weaponsForConfig = group.ToList();
    var score = weaponsForConfig.Sum(w => 
        w.HitProbability * w.Weapon.Damage
    ) * arcBonus;
    
    return new { Configuration = group.Key, Score = score };
}).ToList();

// Take the maximum score across all configurations
var bestConfigScore = configScores.OrderByDescending(c => c.Score).First();
var targetScore = bestConfigScore.Score;
```

When computing `OffensiveIndex` for the entire path:

```csharp
OffensiveIndex = allTargets.Sum(t => t.BestScore);
```

This ensures the offensive potential reflects the **best possible torso rotation** for each target.

**Note:** Defensive index calculation remains unchanged for now (uses leg-facing as it represents armor exposure from the attacker's perspective).

### 4) Stateless decision execution (WeaponsEngine)

**Key principle:** Decision engines are **stateless**. Each `MakeDecision` execution:
- Observes current game state
- Produces exactly **one** command
- Returns (execution ends)
- Next execution is triggered by server command echo (subscription in Bot.cs)

**Workflow for torso-aware weapon attacks:**

#### Execution pattern:
Pseudo code, should be aligned with current implementation, only replacing its corresponding parts
```csharp
public async Task<bool> MakeDecision()
{
    var attacker = SelectAttacker();
    if (attacker == null) return false;
    
    var enemies = GetEnemies();
    var attackerPath = GetAttackerPath(attacker);
    
    // 1. Evaluate all targets with all torso configurations
    var targetScores = _tacticalEvaluator.EvaluateTargets(
        attacker, attackerPath, enemies);
    
    if (!targetScores.Any()) 
    {
        // No viable attacks, declare empty attack to advance phase
        await DeclareNoAttack(attacker);
        return true;
    }
    
    // 2. Find best configuration across all targets
    var bestOption = targetScores
        .SelectMany(t => t.ConfigurationScores)  // All config-score pairs
        .OrderByDescending(cs => cs.Score)
        .First();
    
    var bestConfig = bestOption.Configuration;
    var bestTarget = bestOption.Target;
    
    // 3. Check if configuration needs to be applied
    if (bestConfig.Type == WeaponConfigurationType.TorsoRotation)
    {
        if (!IsConfigurationApplied(attacker, bestConfig))
        {
            // Send configuration command and END execution
            await _clientGame.ConfigureUnitWeapons(
                new WeaponConfigurationCommand(
                    attacker.Id, 
                    bestConfig
                )
            );
            return true; // Execution ends here
        }
    }
    
    // 4. Configuration is applied (or not needed)
    //    Select weapons and declare attack
    var selectedWeapons = SelectWeapons(
        attacker, bestTarget, bestConfig, enemies);
    
    await _clientGame.DeclareWeaponAttack(
        new WeaponAttackDeclarationCommand(
            attacker.Id,
            selectedWeapons
        )
    );
    
    return true; // Execution ends here
}
```

#### Server-driven workflow:

1. **First execution:** Bot evaluates → determines torso rotation needed → sends `WeaponConfigurationCommand` → returns
2. **Server processing:** Calls `BaseGame.OnWeaponConfiguration` → `mech.RotateTorso(direction)` → broadcasts command echo
3. **Second execution (triggered by echo):** Bot re-evaluates → sees torso already rotated → sends `WeaponAttackDeclarationCommand` → returns

**Implementation helpers:**

```csharp
private bool IsConfigurationApplied(IUnit unit, WeaponConfiguration config)
{
    if (config.Type == WeaponConfigurationType.None)
        return true;
        
    if (config.Type == WeaponConfigurationType.TorsoRotation && unit is Mech mech)
    {
        var desiredFacing = (HexDirection)config.Value;
        return mech.Facing == desiredFacing;
    }
    
    return false;
}
```

### 5) Data structure updates

**Extend `TargetScore` (pseudicode, must be adjusted to be aligned with actual types):**

```csharp
public class TargetScore
{
    public IUnit Target { get; set; }
    public double Score { get; set; }
    
    // NEW: Keep all configuration options with their scores
    public List<ConfigurationScore> ConfigurationScores { get; set; }
}

public class ConfigurationScore
{
    public WeaponConfiguration Configuration { get; set; }
    public double Score { get; set; }
    public IUnit Target { get; set; }
    public List<WeaponEvaluationData> ViableWeapons { get; set; }
}
```

The goal is to allow `WeaponsEngine` to:
- See all configuration options
- Pick the globally best configuration
- Access the weapon list for that configuration

## Edge cases and considerations

### 1) Unit is not a mech

- `GetWeaponsConfigurationOptions()` returns empty for non-mechs
- Only `WeaponConfigurationType.None` is evaluated
- Behavior identical to current implementation

### 2) Mech cannot rotate torso

- `CanRotateTorso == false` or `HasUsedTorsoTwist == true`
- `GetWeaponsConfigurationOptions()` returns empty list
- Only `WeaponConfigurationType.None` is evaluated
- Behavior identical to current implementation

### 3) Firing arc computation

- `IsInWeaponFiringArc` accepts explicit `facing` parameter
- Must pass the configuration's facing value, not default to `weapon.FirstMountPart.Facing`
- This avoids requiring actual state mutation during evaluation

### 4) Multiple targets and secondary target modifier

Current bot declares all weapons at a single target. If/when splitting fire:

- Secondary target penalties depend on `AttackScenario.AttackerFacing`
- Must use the chosen configuration's facing (post-rotation), not leg-facing
- Already handled correctly if `CalculateHitProbability` receives `attackerFacing: (HexDirection)config.Value`

### 7) Defensive evaluation and enemy torso twist

Current `CalculateDefensiveIndex` uses actual enemy unit facing state.

**Out of scope for this change:** Assuming enemies can torso-twist to shoot the defender would require analogous "max over enemy configurations" logic in defensive scoring. This could be addressed in future iterations.

## Recommended incremental implementation path

### Step 1: Extend data structures
- Add `Configuration` property to `WeaponEvaluationData`
- Add `ConfigurationScores` to `TargetScore` (or similar changes to support required functionality)

### Step 2: Torso-aware weapon evaluation
- Modify `EvaluateWeaponsForTarget` to:
  - Enumerate configurations via `GetWeaponsConfigurationOptions`
  - Add nested loop over configurations
  - Check `IsWeaponConfigurationApplicable` per weapon
  - Use `config.Value` as facing for arc/scenario calculations
  - Tag each viable weapon with its configuration

### Step 3: Configuration-aware scoring
- Modify `EvaluatePath` or `EvaluateTargets` to:
  - Group weapons by configuration
  - Calculate score per configuration
  - Take max score for offensive index
  - Preserve all configuration scores for `WeaponsEngine`

### Step 4: Weapons engine decision logic
- Modify `WeaponsEngine.MakeDecision` to:
  - Find best configuration across all targets
  - Check `IsConfigurationApplied`
  - Send `WeaponConfigurationCommand` if needed (end execution)
  - Send `WeaponAttackDeclarationCommand` if configuration ready (end execution)

### Step 5: Testing and validation (MakaMek.BotTests project)
- Verify movement evaluation favors positions with torso-twist opportunities
- Verify weapons engine correctly rotates torso when beneficial
- Verify stateless execution pattern (one command per call)
- Test edge cases (non-mechs, already-rotated, rotation-blocked)

## Completion status

This document describes current behavior and provides implementation guidance for torso-aware evaluation and torso rotation commands in bot decision-making. The design has been clarified to reflect:

- Use of existing `GetWeaponsConfigurationOptions()` API
- Configuration tagging at weapon evaluation level
- Grouped scoring by configuration
- Stateless decision engine pattern with server-driven workflow
- Correct usage of `WeaponConfiguration` record structure
