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
- Picks the “best” path by sorting:
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
  - Builds a `targetPath` from the target’s `MovementTaken` (or standing still).
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

`WeaponsAttackState` shows the expected “human” workflow:

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

### A) Offensive evaluation uses the *leg facing*, not a “best possible torso facing”

In `TacticalEvaluator.EvaluateWeaponsForTarget` the arc check passes `attackerPath.Destination.Facing` to `IsInWeaponFiringArc`.

- `HexPosition.Facing` is the unit’s base facing (leg facing)
- Torso twist is a separate override on the torso parts (`Torso.Rotate`) and is exposed through `Mech.Facing`

As a result, when a target is slightly outside the front/side arcs for the leg-facing, the bot assigns **0 viable weapons**, even though a torso rotation would bring the target into arc.

### B) The bot never actually torso-twists during weapons phase

Even if a torso twist would unlock significant damage, `WeaponsEngine` goes straight to `WeaponAttackDeclarationCommand`.

### C) Secondary-target handling is also tied to attacker facing

`AttackScenario` includes `AttackerFacing`, which feeds `SecondaryTargetModifier` calculations in `ToHitCalculator`.

Currently `TacticalEvaluator.CalculateHitProbability` passes `attackerFacing: attackerPath.Destination.Facing`, which again ignores torso twist.

(While the current bot uses only one target, this becomes more important once the bot starts splitting fire.)

## Proposed solution overview

The goal is to treat torso rotation as a **choice variable** in the bot’s evaluation and decision pipeline:

- During movement evaluation, offensive potential should be computed as:
  - `max_over_possible_torso_facings( expected_damage(attacker_position, torso_facing) )`
- During weapons declaration, the bot should:
  - choose torso facing + target + weapons as a joint optimization
  - issue a `WeaponConfigurationCommand` when beneficial
  - then issue `WeaponAttackDeclarationCommand`

## Proposed design: torso-aware evaluation

### 1) Define “possible torso facings” for a mech

Given a mech at a fixed `HexPosition` with base facing `Position.Facing`:

- If unit is not a `Mech`, there are no torso options.
- If mech cannot rotate (`CanRotateTorso == false`), only one option exists:
  - `torsoFacing = Position.Facing`
- If mech can rotate (`CanRotateTorso == true`), compute all facings within `PossibleTorsoRotation` steps:
  - exactly like `WeaponsAttackState.UpdateAvailableDirections()` does

Important: include the “no rotation” option as well.

### 2) Evaluate targets for a specific torso facing (hypothetical)

Introduce the notion of a torso-aware call:

- `EvaluateTargets(attacker, attackerPath, potentialTargets, HexDirection torsoFacing)`

Internally it must:

- Use `torsoFacing` for the firing arc inclusion check.
  - Instead of passing `attackerPath.Destination.Facing`.
- Use `torsoFacing` for `AttackScenario.FromHypothetical(attackerFacing: torsoFacing)`.

Notes:

- This evaluation should be *pure/hypothetical* (no mutation of mech state).
- It should not require sending commands.

### 3) Compute “best offensive index” for a path

Change the conceptual contract of `EvaluatePath` to:

- For a given `(attacker, path)`, compute:
  - `DefensiveIndex` (unchanged)
  - `OffensiveIndex = max_torsoFacing( Sum(TargetScore.Score) )`

Capture the argmax:

- `BestTorsoFacing` (the facing that maximizes offense)
- Possibly `BestTargetScores` for that facing

**Anton:** that should be the only change needed for the contract. The evaluator then will evaluate all possible torso facings and return the best one.

This enables `MovementEngine` to choose positions that are good *when combined with* an available torso twist.

### 4) Why defensive evaluation likely stays unchanged

`CalculateDefensiveIndex` assesses which enemies can shoot the defender and which *defender arc* is exposed.

- Defender arc computation uses `defenderPath.Destination.Facing` (leg-facing) via `GetFiringArcFromPosition`.
- In Classic BT, a torso twist does not change which side/rear armor is being presented by the legs for purposes of “rear exposure” type heuristics.

So, for this specific enhancement, defensive evaluation can remain leg-facing-based.

Ideally we should also account (potential) enemy torso rotation when evaluating defensive index, but that is not as crucial and can be addressed in future iterations.

## Proposed design: torso rotation decision in `WeaponsEngine`

### 1) Joint optimization: torsoFacing + target + weapons

For the current bot behavior (single target), the simplest correct algorithm is:

- Enumerate torso options
- For each torso option:
  - Evaluate targets and pick the best target score
  - Select weapons under heat/ammo constraints
  - Compute “final expected damage” for that plan
- Pick the best plan overall

This replaces the current approach of:

- Evaluate targets once with leg-facing
- Pick best target
- Select weapons

### 2) Command ordering / workflow

A torso-twist is sent as `WeaponConfigurationCommand` and is processed in `WeaponsAttackPhase` without consuming the unit’s action.

Recommended bot workflow per attacker unit:

1. **Plan** (pure evaluation, no commands)
   - Determine best `(torsoFacing, target, selectedWeapons)`.
2. **If needed** and allowed:
   - If attacker is `Mech` and `mech.CanRotateTorso` and `torsoFacing != mech.Position.Facing`:
     - Send `WeaponConfigurationCommand` with `WeaponConfigurationType.TorsoRotation`.
     - `await _clientGame.ConfigureUnitWeapons(command)`.
     - If it fails: fall back to “no rotation” plan.
3. **Declare attack**
   - Build `WeaponTargetData` list and send `WeaponAttackDeclarationCommand`.

### 3) “Gracefully continue to select weapons after torso rotation”

Two safe options exist:

- **Option A (recommended):** choose weapons *before* sending the torso rotation command.
  - You don’t need to re-run selection after rotation, because the evaluation already assumed that torsoFacing.
  - After ack, you immediately declare attack.

- **Option B (more robust):** re-evaluate after rotation ack.
  - Re-run `EvaluateTargets` based on the now-updated unit state and re-select weapons.
  - This is safer if later other mechanics introduce dependencies (e.g., more modifiers based on facing).

**Anton:** it should be option B, as decision engines are stateless, execution ends with a command sent, so once torso rotation command is sent we shold end the execution, the next one should be tirigered by the server echo of the command.

### 4) Integration with client command synchronization

`ClientGame` tracks pending commands and blocks further actions while `_pendingCommands` is not empty (`CanActivePlayerAct`).

Therefore, `WeaponsEngine` must:

- `await` the `ConfigureUnitWeapons` task completion before attempting `DeclareWeaponAttack`.

**Anton:** again it should not be "await" but another MakeDecision execution trigered by the server command

This matches the human UI’s implicit model (configure first, then declare).

## Suggested changes to bot evaluation/decision flow (implementation guidance)

### A) TacticalEvaluator enhancements

Add torso-awareness as a first-class concept:

- **New helper:** `GetPossibleTorsoFacings(IUnit attacker, HexPosition destination)`.
- **New overload or parameter:** `EvaluateTargets(..., HexDirection attackerFacing)`.

Ensure the following lines conceptually switch from leg-facing to torso-facing:

- Arc check:
  - from `attackerPath.Destination.Facing`
  - to `attackerFacing` (torsoFacing candidate)

- `AttackScenario.FromHypothetical(attackerFacing: ...)`:
  - from `attackerPath.Destination.Facing`
  - to `attackerFacing`

### B) MovementEngine enhancements

`MovementEngine` already evaluates many paths concurrently.

- Offensive scoring should incorporate torso options *inside* `EvaluatePath`.
- Optional improvement: store “recommended torso facing” in `PositionScore` for the best-scoring path.
  - This could reduce recomputation in `WeaponsEngine`, but it is not required.

### C) WeaponsEngine enhancements

`WeaponsEngine.MakeDecision` should become a 2-step command sequence when rotating torso:

- `ConfigureUnitWeapons` (optional)
- `DeclareWeaponAttack`

Planning logic should pick the best torso option using the same scoring basis as `TacticalEvaluator`.

## Edge cases and considerations

### 1) Unit is not a mech

- No torso rotation options.
- Behavior remains unchanged.

### 2) Mech cannot rotate torso

- `PossibleTorsoRotation == 0` or `HasUsedTorsoTwist == true`.
- Evaluation should consider only the current facing.

### 3) Torso rotation resets on movement and at turn reset

- `Mech.Position` setter calls `torso.ResetRotation()` whenever position changes.
- `Mech.ResetTurnState()` resets torso rotation.

Implication:

- Movement evaluation can safely assume “torso is neutral” at the end of movement.
- Weapons planning should assume torso twist is available if `CanRotateTorso`.

### 4) Firing arc computation depends on part facing

- `HexCoordinatesExtensions.IsInWeaponFiringArc` uses a `facing` parameter; if null, it defaults to `weapon.FirstMountPart.Facing`.
- Since `UnitPart.Facing` defaults to `Unit.Facing`, and `Mech.Facing` reflects torso direction, actual torso rotation affects firing arcs.

For evaluation, you should prefer the explicit `facing` parameter to avoid requiring mutation.

### 5) Defensive evaluation and enemy torso twist

Current `CalculateDefensiveIndex` uses actual enemy unit facing state (via part-facing defaults).

If you later want to assume enemies *can* torso twist to shoot you, you would need an analogous “max over enemy torso facings” in defense as well. That is **out of scope** for this change, but worth noting.

### 6) Multiple targets and secondary target modifier

Right now the bot declares all selected weapons at a single target.

If/when the bot starts splitting fire:

- Secondary target penalties depend on whether targets are in front arc, using `AttackScenario.AttackerFacing`.
- That facing should be the chosen torso-facing (post-rotation), not the leg-facing.

### 7) Failure handling / graceful degradation

If `ConfigureUnitWeapons` fails (returns false) or is rejected by rules:

- Fall back to “no rotation” plan.
- Still declare weapon attack (possibly empty) to avoid stalling the phase.

## Recommended incremental implementation path

- **Step 1:** Add torso-aware target evaluation primitives (no bot behavior changes yet).
- **Step 2:** Use max-over-torso options in `TacticalEvaluator.EvaluatePath` so movement scoring becomes torso-aware.
- **Step 3:** Update `WeaponsEngine` to:
  - evaluate torso options
  - send `WeaponConfigurationCommand` when beneficial
  - then declare weapon attack
- **Step 4 (optional):** Persist “planned torso facing” from movement evaluation into weapons planning to reduce recomputation.

## Completion status

- This document describes current behavior and provides implementation guidance for torso-aware evaluation and torso rotation commands in bot decision-making.
