# Weapon Attack Resolution Pattern

How the Weapon Attack phase resolves declared attacks across all units and players. The pattern keeps `WeaponAttackResolutionPhase` thin — an orchestrator that flattens the attack space, runs gates, delegates resolution, and finalizes results.

## Why

The phase is decomposed into three distinct layers, mirroring the Movement phase's separation of concerns:

1. **Phase (orchestrator)** — flattens the attack space with `BuildAttackQueue()`, runs optional gates, delegates to the resolver, and publishes commands.
2. **IWeaponAttackResolver** — mandatory sequential dice/damage pipeline (to-hit → direction → LOS/cover → hit location). Every attack passes through the same steps.
3. **IAttackResolutionGate** — optional pre-attack skip conditions (chain-of-responsibility). Currently only `AttackerPartialCoverGate`.

None of these is a literal copy of `IMovementInterruptHandler`. The movement handler chain is **optional any-may-fire** (each handler independently decides whether its hazard applies). The weapon resolution pipeline is **mandatory and fixed-order** — to-hit, direction, LOS, hit location, damage — every attack, always. These are structurally different problems that share a thin‑phase approach, not a common interface.

## Components

```text
WeaponAttackResolutionPhase (thin orchestrator)
    │
    ├─ BuildAttackQueue()              ← flatten players → units → weapons
    │
    ├─ IAttackResolutionGate[]         ← movement-like chain (optional skips)
    │     AttackerPartialCoverGate
    │
    ├─ IWeaponAttackResolver           ← mandatory pipeline (dice + damage)
    │
    ├─ FinalizeAttackResolution()      ← apply damage, publish commands,
    │                                    hull breach, crits, consciousness
    │
    └─ CalculateEndOfPhasePsrs()       ← accumulated PSR checks at phase end
```

### `AttackQueueItem` — the flattened unit of work

```csharp
private readonly record struct AttackQueueItem(
    IPlayer Player,
    IUnit Attacker,
    Weapon? Weapon,
    IUnit? TargetUnit,
    WeaponTargetData WeaponTargetData
);
```

Produced once by `BuildAttackQueue()`. Contains everything needed to resolve a single weapon shot. The queue is built in initiative order (player order from `Game.InitiativeOrder`, then each player's units, then each unit's `DeclaredWeaponTargets`).

### `BuildAttackQueue()` — flattening step

Walks `_playersInOrder`, then each player's units in `_unitsWithTargets`, then each unit's `DeclaredWeaponTargets`. For each weapon target it resolves the mounted `Weapon` (via `unit.GetMountedComponentAtLocation<Weapon>`) and the target `IUnit` (via `Game.Players.SelectMany(p => p.Units)`). The result is a flat `List<AttackQueueItem>` that the main loop iterates.

### `IWeaponAttackResolver` — the mandatory pipeline

```csharp
public interface IWeaponAttackResolver
{
    AttackResolutionData ResolveAttack(
        IUnit attacker,
        IUnit target,
        Weapon weapon,
        WeaponTargetData weaponTargetData,
        IBattleMap battleMap);
}
```

A self-contained, stateless service (constructor-injected `IRulesProvider`, `IDiceRoller`, `IDamageTransferCalculator`, `IToHitCalculator`). Every attack goes through its single public method, which runs:

1. **To-hit** — `IToHitCalculator.GetToHitNumber`, then `IDiceRoller.Roll2D6`.
2. **Miss** — returns early with `IsHit = false`.
3. **Direction** — `DetermineAttackDirection` (firing arc → `HitDirection`).
4. **LOS / partial cover** — `battleMap.GetLineOfSight` + `IRulesProvider.HasPartialCover`.
5. **Hit location** — `DetermineHitLocation` for standard weapons, `ResolveClusterWeaponHit` for clusters (LRMs, SRMs, etc.).

Private helpers `DetermineHitLocation`, `ResolveClusterWeaponHit`, and `DetermineAttackDirection` are tested through the public API or via direct reflection in resolver-specific tests.

### `IAttackResolutionGate` — optional pre-attack skips

```csharp
public interface IAttackResolutionGate
{
    bool ShouldSkip(IUnit attacker, IUnit target, Weapon weapon,
        LocationSlotAssignment primaryAssignment, IBattleMap battleMap, ServerGame game);
}
```

A chain-of-responsibility that mirrors `IMovementInterruptHandler.Check`. Each gate inspects the attack context and returns `true` to skip resolution (with logging), `false` to proceed. Currently registered in the phase:

```csharp
private readonly IReadOnlyList<IAttackResolutionGate> _attackResolutionGates =
[
    new AttackerPartialCoverGate()
];
```

`AttackerPartialCoverGate` implements the leg-weapon partial-cover skip: it reverses LOS (target → attacker) and checks `HasPartialCover` combined with `CanPartBeCovered(primaryAssignment.Location)`.

### `FinalizeAttackResolution()` — post-hit consequences

Stays in the phase because it touches phase-local state (`_accumulatedDamageData`) and publishes commands. Each call:

1. Applies damage to the target via `target.ApplyDamage(...)`.
2. Handles external heat if present.
3. Publishes a `WeaponAttackResolutionCommand` (broadcasts the resolution to clients).
4. Calculates and publishes hull breach (via `Game.HullBreachCalculator`).
5. Calculates and publishes critical hits (via `Game.CriticalHitsCalculator`).
6. Runs `ProcessConsciousnessRollsForUnit`.
7. Accumulates component hits and destroyed parts into `_accumulatedDamageData` for the end-of-phase PSR check.

### `CalculateEndOfPhasePsrs()` — phase-ending PSRs

After all attacks resolve, iterates accumulated damage data and runs `Game.FallProcessor.ProcessPotentialFall` for each mech that took damage during the phase. This enforces the BattleTech rule that PSRs from accumulated damage are rolled at phase end, not per attack.

## Control flow

```
Enter()
  │
  ├─ Clear _accumulatedDamageData
  ├─ Build _playersInOrder from Game.InitiativeOrder
  ├─ PrepareUnitsWithTargets()
  │
  └─ ResolveNextAttack()
       │
       ├─ Guard: Game.BattleMap == null → throw
       │
       ├─ BuildAttackQueue() → List<AttackQueueItem>
       │
       ├─ foreach item in queue:
       │   │
       │   ├─ Guards: weapon != null, target has Position, attacker has Position
       │   │
       │   ├─ Gates: foreach gate in _attackResolutionGates
       │   │   └─ gate.ShouldSkip(...) → true → skip this item
       │   │
       │   ├─ Resolve: _resolver.ResolveAttack(...) → AttackResolutionData
       │   │
       │   └─ Finalize: FinalizeAttackResolution(player, attacker, weapon, target, data)
       │
       ├─ CalculateEndOfPhasePsrs()
       │
       └─ Game.TransitionToNextPhase(Name)
```

Key points:
- `BuildAttackQueue()` runs once. The queue is materialised eagerly, then iterated with a simple `foreach`.
- Gates run before resolution. If any gate returns `true`, the attack is skipped (logged) and the loop advances to the next queue item.
- The resolver is a black box: it receives the full context and returns a complete `AttackResolutionData`. The phase never inspects intermediate pipeline state.
- All post-resolution effects (damage, commands, crits, consciousness) happen in `FinalizeAttackResolution`, keeping the loop body short.

## Worked example: LRM volley against a mech with partial cover

1. **Phase starts**: `Enter()` → clears accumulated damage, builds `_playersInOrder`, calls `PrepareUnitsWithTargets()`.
2. **Queue built**: `BuildAttackQueue()` produces items. One item: Player A's Archer fires LRM-15 at Player B's Wolverine.
3. **Guard check**: weapon is mounted (`Weapon` is not null), Wolverine has `Position`, Archer has `Position` → proceed.
4. **Gate check**: `AttackerPartialCoverGate.ShouldSkip(...)` — Archer's LRM-15 is not a leg-mounted weapon → `false` (do not skip).
5. **Resolver**:
   - `_toHitCalculator.GetToHitNumber` → needs 7+.
   - Roll: 4 + 5 = 9 → hit.
   - `DetermineAttackDirection` → Archer is in Wolverine's front arc → `HitDirection.Front`.
   - LOS check → partial cover detected; covering hex recorded.
   - `ResolveClusterWeaponHit`: cluster roll 8 → 12 missiles hit; 3 clusters of 5 damage resolve against hit locations (partial cover absorption applied per `DetermineHitLocation` where legs are hit).
6. **Finalize**: damage applied to Wolverine, `WeaponAttackResolutionCommand` published, hull breach/crits/consciousness handled.
7. **Next items**: loop continues for remaining queue items.
8. **Phase end**: `CalculateEndOfPhasePsrs()` runs; Wolverine took 30+ damage in the phase → `FallProcessor` emits a PSR command.
9. **Transition**: `Game.TransitionToNextPhase(PhaseNames.Heat)`

## Testing

Three layers of testing, mirroring the Movement phase:

| Layer | What is tested | How |
|-------|---------------|-----|
| **Resolver** (unit) | `ResolveAttack`, `DetermineHitLocation`, `ResolveClusterWeaponHit` | Construct `WeaponAttackResolver` with mocked dependencies, call `ResolveAttack` or invoke private methods via reflection. Tests live in `WeaponAttackResolverTests`. |
| **Gates** (unit) | `AttackerPartialCoverGate.ShouldSkip` | Construct the gate, call `ShouldSkip` with controlled game/map state. Tests live in `AttackerPartialCoverGateTests`. |
| **Phase** (integration) | `Enter()` → full resolution flow, queue ordering, gate interaction, command publishing | Set up `ServerGame` with players/units/declared targets, call `Enter()`, assert commands published. Tests live in `WeaponAttackResolutionPhaseTests`. |

## Extension guide

### Adding a new pre-attack gate

1. Implement `IAttackResolutionGate`. Return `true` when the attack should be skipped.
2. Register the handler in `WeaponAttackResolutionPhase._attackResolutionGates` at the correct position in the chain.
3. Write a unit test calling `ShouldSkip` with the conditions that should trigger (and should not) skip.

### Adding a new step to the resolution pipeline

The pipeline inside `WeaponAttackResolver` is **not** pluggable by design (see rationale below). To add a new step:

1. Add the step call inside `WeaponAttackResolver.ResolveAttack` at the appropriate point in the sequence.
2. If the new step requires additional dependencies, add them to the constructor.
3. Update `AttackResolutionData` to carry any new output if the step produces phase-visible data.
4. Write unit tests against `WeaponAttackResolver.ResolveAttack`.

### Adding a post-hit consequence

Add the logic in `FinalizeAttackResolution` (or extract it into a dedicated finalizer class if the consequence set grows significantly). Follow the existing pattern: mutate game state, publish commands, update `_accumulatedDamageData`. Write tests against phase-level `Enter()` or, if extracted, against the finalizer in isolation.

For future reference only: if post-hit ordering becomes fragile or additional post-hit effects are introduced, extract the discrete side-effects of `FinalizeAttackResolution` (apply damage/heat, publish `WeaponAttackResolutionCommand`, hull breach, critical hits, consciousness rolls) into `IGameAction` implementations under `Mechanics/WeaponAttack/Actions/`, executed via the canonical `ProcessInterruptResult`-style loop (accumulate commands from `action.Process(Game)`, then publish in order). For this ticket, leave `FinalizeAttackResolution` intact in the phase, unchanged.

## Rationale: Why not `IMovementInterruptHandler` wholesale?

| Concern | Movement interrupts | Weapon resolution |
|---------|-------------------|-------------------|
| Chain type | Optional — each handler may or may not fire per segment | Mandatory — every attack passes every step |
| Order | Any handler can stop the chain | Fixed order (to-hit → direction → LOS → location) |
| Result | `null` (no fire) or `MovementInterruptResult` (actions + stop flag) | Single `AttackResolutionData` — miss, hit, or cluster |
| Central mutation | Handlers emit `IGameAction`s; phase runs them | Resolver returns data; phase applies damage + commands |

The interrupt handler chain models **independent hazard checks** where any one may short-circuit. The weapon resolution pipeline models a **sequential calculation** where each step depends on the previous. These differ in control flow, result type, and composition. Using the same interface for both would produce leaky abstractions — gates that return "skip the attack" (a natural fit for `IMovementInterruptHandler`) are already covered by `IAttackResolutionGate`.

## Out of scope

Approaches E (full `IAttackResolutionStep` pipeline) and F (unified cross-phase framework) from the refactoring analysis are intentionally out of scope. The current three-layer split (orchestrator → resolver → gates) provides the right balance of testability, readability, and extensibility for the foreseeable feature set. If future needs (such as AMS interception, physical attack integration, or phased resolution ordering) demand further decomposition, the pattern can be extended without breaking the existing layers.
