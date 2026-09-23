# Physical Attacks — Product Requirements Document

**Date:** 2026-09-23
**Status:** Draft (feature spec for refinement into GitHub issues)

---

## Executive Summary

This document specifies the requirements for implementing 'Mech physical attacks (punch, kick, club, push, charge, DFA, physical weapons) in MakaMek. Physical attacks reuse the proven two-phase declaration/resolution architecture of weapon attacks but use the **Piloting Skill** as the base to-hit value and follow BattleTech rules that differ meaningfully in timing (Charge/DFA are declared during Movement), damage (tonnage-derived), hit locations (dedicated Punch/Kick tables), and PSR handling.

The implementation extends the existing phase pipeline (`WeaponsAttack → WeaponAttackResolution → **PhysicalAttack** → Heat`), adds a `PhysicalAttackResolutionPhase` modeled on the Weapon-Attack-Resolution-Pattern, and reuses existing mechanics (to-hit modifiers, PSR contexts, fall processing, displacement) wherever possible.

Work is split into milestones so that each can be refined into independently-shippable issues:
- **M1** — Punch & Kick (core mechanics + resolution)
- **M2** — Push (displacement + advance)
- **M3** — Charge & DFA (Movement-phase declaration)
- **M4** — Physical weapons (hatchet/sword)
- **M5** — Club (improvised objects)

---

## Background — BattleTech Rules Reference

The authoritative rules source for this feature is [`docs/rules/physical-attacks.md`](../rules/physical-attacks.md). Key rules the implementation must satisfy:

1. **Phase timing** — Punch, Kick, Push are declared and resolved in the **Physical Attack Phase** (after Weapon Attack Resolution, before Heat). **Charge and DFA are declared during the Movement Phase** (they dictate movement) but resolved in the Physical Attack Phase.
2. **Base to-hit** — attacker's **Piloting Skill** (not Gunnery). Movement and terrain modifiers apply as usual. **Heat and sensor-damage modifiers never apply** to physical attacks.
3. **Single attack limit** — a 'Mech may perform only **one type** of physical attack per turn.
4. **Damage & hit locations:**

| Attack | Base Mod | Damage | Hit Location |
|--------|----------|--------|--------------|
| Punch | +0 | ⌈Tonnage/10⌉ | Punch table (each arm rolled separately, max 1 or 2 arms) |
| Kick | −2 | ⌈Tonnage/5⌉ | Kick table (legs); PSR for target on hit, PSR for attacker on miss |
| Club | −1 | ⌈Tonnage/5⌉ | Standard table; improvised object (severed limb, tree, girder); both arms with undamaged shoulders **and hands**, no arm weapons fired from either arm |
| Push | −1 | 0 | Displacement + target PSR (see §4 of rules doc) |
| Physical weapon (hatchet/sword) | −1 / −2 | ⌈Tonnage/5⌉ / ⌈Tonnage/10⌉ + 1 | Standard table; weapon arm only; optional punch/kick table with +4 |
| Charge | +0 | ⌈Tonnage/10⌉ × hexes moved (5-pt clusters); attacker takes ⌈target tonnage/10⌉ | Displaces target; both PSR (+2) on hit |
| DFA | +0 | ⌈Attacker tonnage/10⌉ × 3 to target (punch table); attacker takes ⌈tonnage/5⌉ to legs on hit | Attacker PSR +4, target PSR +2 on hit; attacker auto-falls on miss |

5. **Weapon-firing restrictions** — a limb with weapons fired during the Weapon Attack Phase cannot punch/kick; a kicking leg's weapons cannot fire that turn; push requires both arms with no arm weapons fired. **Club** requires undamaged shoulder **and hand** actuators in both arms and no arm weapons fired from either arm (carrying a club occupies both hands). **Physical weapons** (hatchet/sword) restrict only the single weapon arm.
6. **Actuator damage** — punch blocked by damaged **shoulder** actuator; club blocked by damaged shoulder or hand actuator in either arm; push is allowed but adds **+2 per damaged shoulder**.
7. **Push specifics** — target must be a standing 'Mech at the exact same elevation in the hex directly in front of the attacker's **feet** (not torso twist); attacker advances into the vacated hex at 0 MP; pushed target PSRs or falls; blocked destination → no movement but PSR still occurs; one displacement (Charge/DFA/Push) per target per turn; mutual pushes resolved per rules.
8. **Eligibility** — prone 'Mechs cannot perform standard physical attacks (punching vehicles / thrashing infantry is out of scope — no such unit types yet); only 'Mechs are attackers and targets in v1.

---

## Current State (Gap)

What already exists:

| Item | Status |
|------|--------|
| `PhaseNames.PhysicalAttack` | Exists but **not wired** — `BattleTechPhaseManager.GetNextPhase` maps `WeaponAttackResolution → Heat` directly (`src/MakaMek.Core/Models/Game/Phases/BattleTechPhaseManager.cs:23`) |
| `PhysicalAttackPhase` | Skeleton only: accepts `PhysicalAttackCommand`, broadcasts it; calls `Game.OnPhysicalAttack` which logs "Physical attacks are not implemented" (`src/MakaMek.Core/Models/Game/BaseGame.cs:368`) |
| `PhysicalAttackCommand` | Declared (`PhysicalAttackType` + attacker/target ids) but has no arm/limb payload and no server-side validation |
| `PhysicalAttackType` enum | `Punch, Kick, Push, Charge, DFA` (`src/MakaMek.Core/Models/Game/PhysicalAttackType.cs`) |
| Resolution mechanics | **None** — no physical to-hit calculation, no punch/kick hit-location tables, no tonnage-derived damage, no physical PSR contexts |
| UI / UiStates | **None** — no `PhysicalAttackState`; `BattleMapViewModel` transitions do not handle the phase |
| Bots | No physical-attack decision engine |

What is missing and must be built is detailed in the requirements below.

---

## Scope

### In Scope

| # | Capability |
|---|-----------|
| 1 | Phase pipeline wiring: `WeaponAttackResolution → PhysicalAttack → Heat` |
| 2 | `PhysicalAttackResolutionPhase` orchestrator following the Weapon-Attack-Resolution-Pattern (attack queue → gates → resolvers) |
| 3 | Physical to-hit calculation (Piloting base, per-type modifiers, no heat/sensor modifiers, movement + terrain modifiers) |
| 4 | Punch and Kick: eligibility, damage, punch/kick hit-location tables, hit/miss PSRs |
| 5 | Push: displacement, attacker advance, target PSR, blocked-destination rules, mutual pushes |
| 6 | Charge & DFA: declaration during Movement Phase, path-based damage, displacement, PSRs, fall-on-miss handling |
| 7 | Per-turn "one physical attack type" and weapon-firing restrictions tracking on units |
| 8 | `PhysicalAttackResolutionCommand` + related server commands (fall, displacement, dice) broadcast to clients |
| 9 | New `PhysicalAttackState` UI state with to-hit preview and target highlighting |
| 10 | Localization for all new commands, modifiers, and UI labels |
| 11 | Physical weapons (hatchet already exists as a component; sword to be added) |
| 12 | Club: improvised-object attack with pickup mechanic (v1 source: severed limbs on the map) |

### Out of Scope (v1)

- Vehicles, ProtoMechs, infantry, battle armor as attackers or targets (includes vehicle ramming, 'Mech thrashing, and punching a vehicle in the attacker's hex)
- Prone 'Mech melee special attacks
- Retractable blade / salvage arm interactions (components do not exist yet)
- Bot decision engines for physical attacks (follow-up PRD)
- Club sourcing beyond severed limbs (tree uprooting and girder salvage from destroyed buildings require terrain/destruction features not yet present)

---

## Milestones

Each milestone is intended to become a small set of GitHub issues.

### M1 — Punch & Kick (foundation)

Delivers the phase wiring, unit-state tracking, to-hit calculation, damage, hit-location tables, resolution phase, and client broadcast. Punch/Kick only. This milestone makes physical attacks *playable* (locally) for the two most common attack types.

### M2 — Push

Displacement mechanics, attacker advance, push PSR, target-elevation/facing validation, mutual-push resolution.

### M3 — Charge & DFA

Movement-phase declaration UX, damage from path length, displacement of the target, attacker collision damage, dual PSRs, miss outcomes (attacker lands aside / DFA attacker auto-falls).

### M4 — Physical weapons

Hatchet and sword as declared attacks with the standard hit-location table.

### M5 — Club

Club as a standalone attack mode using **improvised battlefield objects**. Requires a club pickup/drop mechanic; v1 supports picking up severed 'Mech limbs (arms/legs blown off by critical hits) lying in the attacker's hex. Tree and girder sources are deferred (see Out of Scope) but the data model keeps the door open for them.

> M3 depends on M1 (phase wiring). M2 depends on M1. M4 is independent of M2/M3. M5 depends on M1 (and benefits from critical-hit limb-blown-off data used by `CriticalHitsResolutionCommand`).

---

## Architecture Overview

The feature follows the documented architecture patterns:

- **Two-phase flow** — declaration phase (`MainGamePhase` with turn-order alternation) + resolution phase (server-driven, no player input). This mirrors `WeaponsAttackPhase` → `WeaponAttackResolutionPhase`.
- **Weapon-Attack-Resolution-Pattern** — `PhysicalAttackResolutionPhase` is a phase-as-orchestrator that builds an attack queue (`BuildPhysicalAttackQueue()`), applies pre-attack **gates** (`IAttackResolutionGate`), and delegates resolution to mechanics services. See `docs/architecture/Weapon-Attack-Resolution-Pattern.md`.
- **Commands as the only state changes** — declaration via `PhysicalAttackCommand` (client), resolution via new `PhysicalAttackResolutionCommand` (server), displacement via existing `DisplaceUnitCommand`, falls via existing `MechFallCommand`, dice via existing `DiceRolledCommand`.
- **Mechanics as calculator services** — a new `IPhysicalAttackResolver` in `Models/Game/Mechanics/` mirrors `IWeaponAttackResolver`; to-hit via a new physical-attack calculator built on `ToHitCalculator`'s modifier pipeline; PSRs via existing `IPilotingSkillCalculator` + `IFallProcessor`.
- **Source generators** — new `RollModifier` subclasses and `PilotingSkillRollContext` records are picked up automatically by the generated registries (`RollModifierTypeResolverGenerator`, `PilotingSkillRollContextTypeResolverGenerator`). Do not hand-maintain switches. New command types are likewise auto-registered.
- **Presentation layer** — new `PhysicalAttackState : IUiState` in `MakaMek.Presentation/UiStates` mirroring `WeaponsAttackState`'s step machine; `BattleMapViewModel.TransitionToState` gains a `PhaseNames.PhysicalAttack` case.

### Phase Wiring

**File:** `src/MakaMek.Core/Models/Game/Phases/BattleTechPhaseManager.cs`

```
PhaseNames.WeaponAttackResolution => new PhysicalAttackPhase(game),
PhaseNames.PhysicalAttack => new PhysicalAttackResolutionPhase(game),
PhaseNames.PhysicalAttackResolution => new HeatPhase(game),
```

Requires a new `PhaseNames.PhysicalAttackResolution` value. `PhysicalAttackPhase` stays a declaration phase (existing skeleton, strengthened with validation). All new `PhysicalAttackResolution*` behavior is introduced in M1.

> **Note:** the existing `PhysicalAttackPhase` maps 1:1 to the declaration half. A separate `PhysicalAttackResolutionPhase` keeps the orchestrator/resolver code out of the player-driven phase, exactly as weapons do.

---

## Detailed Requirements

### R1 — Unit State & Declaration Validation (M1)

**Files:** `src/MakaMek.Core/Models/Units/IUnit.cs`, `Unit.cs`, `Mechs/Mech.cs`, `UnitWeaponAttackState.cs` (pattern reference)

1.1. Add a physical-attack declaration state to `IUnit`/`Unit`, analogous to `HasDeclaredWeaponAttack` / `DeclaredWeaponTargets`:
    - `bool HasDeclaredPhysicalAttack { get; }`
    - `PhysicalAttackDeclaration? DeclaredPhysicalAttack { get; }` — record carrying `AttackType`, optional limb (left/right arm for punch; which leg for kick is implied by target hex/orientation), and target id.
    - Cleared in `ResetPhaseState()` (which already runs on each phase transition).

1.2. **Fired-weapons-per-location tracking.** Currently `Unit.FireWeapon` only consumes ammo; there is no per-turn record of *where* weapons were fired. Add `IReadOnlySet<PartLocation> FiredWeaponLocations` populated in `FireWeapon` and cleared in `ResetPhaseState()`. Physical attack eligibility (punch arm, kicking leg, push arms) is validated against this set.

1.3. `PhysicalAttackCommand` is extended with the fields needed for validation:
    - `PartLocation? AttackerLimb` — required for punch (LeftArm/RightArm), optional for kick (leg selected by attack geometry), not used for push.
    - Kept a client unit command (`IClientUnitCommand`) with `IdempotencyKey`.

1.4. Server-side validation in `PhysicalAttackPhase.HandleCommand` (via `ServerGame.OnPhysicalAttack`):
    - command originates from the active player of the step (`PhaseStepState.ActivePlayer`),
    - attacker is a Mech, not prone, not destroyed, not shut down,
    - attacker has not yet declared a physical attack this turn (single attack limit),
    - attack type is eligible per M1 (punch/kick) rules: limb weapons not fired, shoulder actuator intact (punch), hip actuators intact + leg weapons not fired (kick), attacker not skidding,
    - target is a valid enemy unit (adjacent 'Mech; adjacency and arc checks per attack type are validated again at resolution time against current positions).
    - Invalid commands return an `ErrorCommand` (existing idempotency-aware pattern used by `MovementPhase`).

1.5. A skipped declaration is supported: a unit can end its physical attack step without declaring (UI "skip" action sends no command; turn order advances via the existing `HandleUnitAction` unit counter). An explicit `SkipPhysicalAttackCommand` is **not** required if the turn-advance logic tolerates zero declarations — see Open Questions.

### R2 — Physical To-Hit Calculation (M1)

**Files:** new `Models/Game/Mechanics/PhysicalAttack/` folder; `Data/Game/Mechanics/AttackScenario.cs`; `Rules/IRulesProvider.cs`, `TotalWarfareRulesProvider.cs`

2.1. Introduce a physical-attack scenario record (`PhysicalAttackScenario`) analogous to `AttackScenario` but keyed on **Piloting Skill** (`AttackerPiloting`) and `PhysicalAttackType`. It excludes heat and sensor modifiers by construction.

2.2. New calculator interface `IPhysicalAttackCalculator` with `GetToHitNumber(...)` / `GetModifierBreakdown(...)` returning `ToHitBreakdown` (reused record). Implementation composes existing `RollModifier` instances:

    - New modifiers (each a `RollModifier` subclass, auto-registered by the source generator):
      - `PhysicalAttackBaseModifier` (attack-type base: Punch +0, Kick −2, Club −1, Push −1, Physical weapon −1/−2, Charge +0, DFA +0)
      - `ChargePilotingDifferenceModifier` (relative piloting skill, M3)
      - `DfaJumpModifier` (M3)
      - `PushDamagedShoulderModifier` (+2 per damaged shoulder, M2)
    - Reused modifiers where rules allow: `TargetMovementModifier`, `TerrainRollModifier` (attacker movement/terrain modifiers follow the standard movement tables — physical attacks use the attacker's executed movement type).

2.3. Extend `IRulesProvider` with the physical hit-location tables:
    - `PartLocation GetPunchHitLocation(int diceResult, HitDirection attackDirection)`
    - `PartLocation GetKickHitLocation(int diceResult)`
    Implemented in `TotalWarfareRulesProvider` per the classic tables (punch: 3–4 Right Arm, 5 Right Leg, 6 Right Torso, 7 CT, 8 Left Torso, 9 Left Leg, 10–11 Left Arm, 12 Head, 2 = CT critical; kick: 2–8 Right Leg, 9–12 Left Leg variants).

2.4. To-hit preview API must be exposed for UI/bot use without mutating state (same approach as `GetModifierBreakdown` overloads used by bots).

### R3 — Damage & Hit Location Resolution (M1)

**Files:** new `Models/Game/Mechanics/PhysicalAttack/IPhysicalAttackResolver.cs`, `PhysicalAttackResolver.cs`; `Data/Game/AttackResolutionData.cs`

3.1. `IPhysicalAttackResolver.ResolveAttack(PhysicalAttackContext)` mirrors `IWeaponAttackResolver.ResolveAttack` and returns `AttackResolutionData` (reuse: `IsHit`, `HitLocationsData`, `AttackDirection`, dice/PSR roll data). New context record carries attacker, target, attack type, limb, and map.

3.2. Damage values computed from attacker tonnage:
    - Punch: ⌈tonnage/10⌉ per arm punch
    - Kick: ⌈tonnage/5⌉
    - Applied through the same damage pipeline as weapon hits (`target.ApplyDamage(hitLocations, attackDirection)`), so armor→internal→destruction transfer, destroyed-part tracking, and critical-hit generation follow existing `Mechanics.DamageTransferCalculator` / `ICriticalHitsCalculator` behavior.

3.3. Hit location:
    - Punch → `GetPunchHitLocation` (attack direction derived from attacker→target hex geometry; each declared arm is a separate roll in the queue).
    - Kick → `GetKickHitLocation`.

3.4. On a **miss**, no damage and no target PSR. Kick-specific miss PSR (attacker falls) is handled by R4.

### R4 — Physical Attack Resolution Phase (M1)

**Files:** new `Phases/PhysicalAttackResolutionPhase.cs`; reuses `Mechanics/Mechs/Falling/*`, `Mechanics/Movement/Actions/*`

4.1. `PhysicalAttackResolutionPhase : GamePhase` with `Enter()` orchestration:

    - Build queue from all units with `HasDeclaredPhysicalAttack`, in initiative order, one queue item per declared attack (punch arms enqueue separately).
    - Pre-attack gates (new `IAttackResolutionGate` implementations in `Mechanics/PhysicalAttack/`):
      - `PhysicalAttackTargetValidityGate` — re-validates adjacency, arc, elevation (push), target alive/standing at resolution time.
      - `AttackerProneGate` — attacker that became prone during weapon attack resolution cannot perform the physical attack.
      - `AttackerDestroyedGate`.
    - Resolve each queued attack via `IPhysicalAttackResolver`; publish `PhysicalAttackResolutionCommand` per attack (mirrors `WeaponAttackResolutionCommand` fields: player, attacker, target, attack type, limb, resolution data).
    - **Simultaneous damage principle:** all attacks are resolved in queue order, damage applied as each is processed, and all resulting PSRs are rolled at end of phase (matching the rules doc and the existing `_accumulatedDamageData` pattern from `WeaponAttackResolutionPhase`).

4.2. **End-of-phase PSR collection** (reuse and generalize the accumulated-damage approach):
    - Track per-target component hits and destroyed parts (for damage-caused fall PSRs) exactly like `WeaponAttackResolutionPhase.CalculateEndOfPhasePsrs`.
    - Track attack-specific PSR requirements generated during resolution: kick hit → target PSR; kick miss → attacker PSR; push hit → target PSR. Process these after damage-fall PSRs, in initiative order, publishing `MechFallCommand` + follow-up `CriticalHitsResolutionCommand` and consciousness rolls through the existing `IFallProcessor.ProcessMovementAttempt` path with **new PSR contexts**:
      - `PilotingSkillRollType.PhysicalAttackPush` (M2)
      - `PilotingSkillRollType.PhysicalAttackKick` (kick hit/miss; modifier context distinguishes attacker/target)
      - M3: `ChargeImpact`, `DfaLanding`, `DfaMissFall`

4.3. Phase transition: after all resolutions and PSRs, `Game.TransitionToNextPhase(Name)` → Heat. The phase's `Enter()` must handle the "no declarations" case by transitioning immediately (no attacks possible).

4.4. Client rendering: `ClientGame` applies `PhysicalAttackResolutionCommand` data (display-only, same pattern as weapon resolution: no re-computation of damage on clients — server data is authoritative).

### R4 — Displacement: Push (M2)

**Files:** reuses `Data/Game/Commands/Server/DisplaceUnitCommand.cs`, `Mechanics/Movement/Actions/DisplaceUnitAction.cs`; new push validation helpers

5.1. Push validation at declaration *and* resolution:
    - Target is a standing 'Mech, not performing Charge/DFA (M3 interplay).
    - Target in the hex **directly in front of the attacker's feet** (attacker facing, not torso twist — use `Unit.Facing`, ignoring any twist).
    - Attacker and target at the exact same elevation.
    - Attacker has both arms; no arm weapons fired (`FiredWeaponLocations`).
    - +2 per damaged shoulder actuator (not a blocker).
    - One displacement per target per turn: maintain a per-phase set of displaced target ids (Charge/DFA/Push); a second displacement declaration is rejected.

5.2. On hit (damage 0):
    - Target displaced 1 hex directly away (`DisplaceUnitAction`), attacker moves into the vacated hex at 0 MP cost (server-side position update via existing displacement/position command path).
    - Blocked/prohibited destination (prohibited terrain, or blocked by elevation) → **neither unit moves**, but the target PSR still occurs. Being pushed more than 2 levels down is allowed and results in an automatic fall.
    - Target PSR via `PhysicalAttackPush` roll context; fall handled by `IFallProcessor`.

5.3. Mutual pushes: if both sides declared pushes against each other and both hit — neither moves, both PSR. If only one hits — standard push. If both miss — nothing. Implement as a post-resolution interaction check in the phase orchestrator (the attack queue detects reciprocal push pairs before displacement application).

### R5 — Charge & DFA (M3)

**Files:** `Phases/MovementPhase.cs`, `Data/Game/Commands/Client/MoveUnitCommand.cs` (extension), `Mechanics/PhysicalAttack/*`

6.1. **Declaration during Movement Phase.** A charge/DFA is declared as part of the unit's movement:
    - Extend `MoveUnitCommand` (or add a sibling command) with an optional `ChargeTargetId`/`DfaTargetId` and the executed movement path (already serialized for movement).
    - `HexesMoved` for charge damage = hexes traversed **before entering the target's hex** (path data is retained on the unit's `MovementTaken` / command).
    - Target must be in the hex the movement path terminates in (DFA: jump path ending on the target hex).
    - Units that declared Charge/DFA cannot fire weapons this turn and cannot perform physical-phase attacks.

6.2. **To-hit:** Piloting base + relative piloting skill modifier (attacker piloting − target piloting) + jump modifier (DFA); standard target movement/terrain modifiers do not apply to the charge impact roll beyond the piloting-difference rule per the rules doc.

6.3. **Damage:** in 5-point clusters (`⌈Tonnage/10⌉ × hexesMoved` for charge target; DFA target uses punch table with ⌈tonnage/10⌉ × 3). Cluster application reuses existing 5-pt grouping logic (see `FallingDamageCalculator` for the cluster pattern).

6.4. **Outcomes:**
    - Charge hit: target displaced to an adjacent valid hex (existing displacement rules), attacker occupies target hex; target PSR (+2), attacker PSR (+2).
    - Charge miss: attacker occupies hex left/right of its forward arc (alternating/nearest-first selection), no damage, no PSRs.
    - DFA hit: attacker lands in target hex, target displaced; target PSR (+2), attacker PSR (+4); attacker takes ⌈own tonnage/5⌉ to legs.
    - DFA miss: attacker automatically falls (2-level fall, rear-direction damage, standard fall damage pipeline); target is displaced to an adjacent passable hex of its choice — with a server-driven bot/human selection step, or nearest-valid selection in v1 (see Open Questions).
    - DFA collision with target destroyed → attacker falls into the hex.

6.5. Displacement uses the existing `DisplaceUnitCommand` pipeline so network propagation and rendering come for free.

### R6 — Physical Weapons (M4)

6.1. Sword component (`Models/Units/Components/Weapons/Melee/Sword.cs`) following `Hatchet`; range bracket data as needed for any incidental behavior is not used by melee attacks.

6.2. Physical weapon attack declared like punch/kick; to-hit base modifier hatchet −1 / sword −2; damage ⌈tonnage/5⌉ (hatchet), ⌈tonnage/10⌉ + 1 (sword); hit location via standard table; weapons on the weapon-arm cannot fire.

6.3. Punch/kick-table option (+4 modifier) is a UI toggle when the mech has a physical weapon; included only if trivially supported by the scenario record — otherwise deferred (Open Questions).

### R7 — Club (M5)

**Files:** `Models/Units/Components/Weapons/Melee/` (new `Club.cs` improvised-item class), `Mechs/Mech.cs` (carrying state), `CriticalHitsCalculator` (limb removal hooks), `PhysicalAttackState` (pickup UI)

7.1. **Club is a distinct attack mode, not a physical-weapon variant.** `PhysicalAttackType.Club` is added as its own enum value; its eligibility, damage, and hit-location rules are separate from hatchet/sword.

7.2. **Club item model:**
    - New `Club` improvised item (not a mounted weapon): `Id`, `SourceType` (SeveredLimb / Tree / Girder — only `SeveredLimb` in v1), `SourcePartLocation` (limb it came from), carried-by reference.
    - When an arm or leg is blown off by a critical hit (`IsBlownOff` in `CriticalHitsResolutionCommand`), the severed limb becomes a club item present in the hex at the target's location.
    - A 'Mech in the same hex as a club item can **pick it up** (physical-phase UI action, no MP cost per classic rules); carrying occupies **both hands** — one club per 'Mech, and the 'Mech cannot punch, push, or use physical weapons while carrying.
    - The club is dropped back into the attacker's hex when: the attack completes, the attacker falls, a carrying arm is destroyed, or the 'Mech dies. (Rules don't allow carrying clubs between turns — confirm behavior for turn end in refinement; recommend: club persists in the hex but is carried only during the turn of pickup, dropped at phase end.)

7.3. **Attack rules:**
    - To-hit base −1, damage ⌈tonnage/5⌉, hit location via the **standard** hit-location table (no punch/kick-table option).
    - Eligibility: undamaged shoulder **and hand** actuators in **both** arms; no arm weapons fired from either arm; attacker standing; target is an adjacent enemy 'Mech.
    - Unlike hatchet/sword, the club is not tied to a specific arm — it swings with both.

7.4. The swing resolves like a punch-family attack in the resolution queue (single queue item; no per-arm splits).

### R8 — UI (M1 for punch/kick; M2/M3/M5 extend)

**Files:** `src/MakaMek.Presentation/UiStates/PhysicalAttackState.cs` (new), `PhysicalAttackStep.cs` (new), `ViewModels/BattleMapViewModel.cs`

8.1. `PhysicalAttackState : IUiState` mirroring `WeaponsAttackState`:

    - Steps: `SelectingUnit → ActionSelection → TargetSelection` (punch/kick/push do not need a weapons-configuration step).
    - Action selection offers available attack types for the selected unit (computed from eligibility rules in R1); unavailable types are hidden, not disabled, with a reason label where useful.
    - Target selection highlights valid target hexes (same adjacency/arc/elevation rules as validation).
    - To-hit preview (number + modifier breakdown) shown for the highlighted target — via the R2.4 preview API.
    - "Skip" advances the turn order without a command.

8.2. `BattleMapViewModel` transition map gains `PhaseNames.PhysicalAttack → new PhysicalAttackState(this)` (and `PhysicalAttackResolution` behaves like `WeaponAttackResolution` — passive watching, no `IUiState`).

8.3. Resolution display: game-log rendering of `PhysicalAttackResolutionCommand` (localized), to-hit/dice/PSR entries consistent with weapon resolution rendering.

8.4. All user-facing strings localized in `MakaMek.Localization` (command render strings, modifier names, UI labels) following existing `Command_WeaponAttack*` conventions.

### R9 — Network & Serialization (M1)

9.1. All new/extended commands implement `IGameCommand` serialization used by the transport layer; `PhysicalAttackCommand` idempotency key semantics match other client commands.

9.2. `ServerGame` publishes: declaration (`PhysicalAttackCommand`), resolution (`PhysicalAttackResolutionCommand`), plus existing fall/displacement/critical-hit/consciousness commands. Command order in the log must match the chronology used by `WeaponAttackResolutionPhase` (resolution → criticals → consciousness → PSR falls).

9.3. SignalR transport needs no changes — commands are generic records on the wire; the generated command registry handles dispatch.

---

## Data Model Summary

| Type | Kind | Change |
|------|------|--------|
| `PhysicalAttackType` | enum | exists; M4 adds `PhysicalWeapon`, M5 adds `Club` |
| `PhaseNames` | enum | add `PhysicalAttackResolution` |
| `PhysicalAttackCommand` | client command | extend with limb, validation |
| `PhysicalAttackDeclaration` | record (new, Core) | attacker declaration state |
| `PhysicalAttackResolutionCommand` | server command (new) | resolution broadcast |
| `ClubItem` (source type, carried-by state, hex presence) | record/service (new, Core) | improvised club pickup/drop lifecycle |
| `PhysicalAttackScenario` / `PhysicalAttackContext` | records (new, Mechanics) | to-hit + resolution inputs |
| `PhysicalAttackBaseModifier`, `ChargePilotingDifferenceModifier`, `DfaJumpModifier`, `PushDamagedShoulderModifier` | RollModifiers (new) | source-generated registry |
| `PilotingSkillRollType.*` | enum + context records (new) | kick/push/charge/DFA PSRs |
| `IRulesProvider.GetPunchHitLocation / GetKickHitLocation` | interface (extend) | TotalWarfareRulesProvider implementation |
| `IUnit.HasDeclaredPhysicalAttack / DeclaredPhysicalAttack / FiredWeaponLocations` | unit state (new) | reset per phase |

---

## Testing Requirements

- Unit tests in `tests/MakaMek.Core.Tests` for: to-hit breakdowns per attack type (piloting base, no heat/sensor modifiers, movement/terrain applied), damage math (rounding, charge clusters, hexes-moved counting), punch/kick hit-location tables (all 2–12 results × directions), eligibility validation (single-attack limit, limb weapon restrictions, actuator damage rules), push displacement matrix (elevation, facing, blocked destination, mutual pushes), PSR contexts and end-of-phase PSR ordering, charge/DFA outcomes (hit/miss paths).
- Presentation tests in `tests/MakaMek.Presentation.Tests` for `PhysicalAttackState` step machine and eligibility computation, mirroring existing `WeaponsAttackState` tests.
- No Avalonia tests (per repo convention — logic lives in Core/Presentation).
- Coverage: new Core code must be covered per the `coverage-check` skill / CI gates.

---

## Acceptance Criteria

**M1**
- [ ] Phase order is `… WeaponAttackResolution → PhysicalAttack → PhysicalAttackResolution → Heat` and existing turn flow is unaffected.
- [ ] A standing 'Mech can punch (either arm) and kick an adjacent enemy 'Mech with correct to-hit number, damage, and hit locations; damage appears on the client simultaneously with the log entry.
- [ ] Units with fired arm/leg weapons cannot punch/kick with those limbs; hip/shoulder damage rules enforced.
- [ ] Kick hit → target PSR; kick miss → attacker PSR; falls apply standard fall damage and criticals.
- [ ] Turn order alternates correctly including skipped units; no physical attack → phase completes without commands.

**M2**
- [ ] Push meets all §4 rules-doc requirements; displacement + attacker advance work at 0 MP; blocked destination keeps both units in place but still triggers the target PSR.
- [ ] One displacement per target per turn is enforced.

**M3**
- [ ] Charge/DFA can be declared with movement; damage, displacement, and PSR modifiers (+2/+2, +4/+2) match the rules doc; miss outcomes (side-hex landing / DFA auto-fall with target choice displacement) behave as specified.
- [ ] Charging/DFA units cannot fire weapons or perform physical-phase attacks that turn.

**M4**
- [ ] Hatchet/sword attacks with correct modifiers, damage, and hit locations; weapon-arm firing restriction enforced.

**M5**
- [ ] A blown-off arm/leg appears as a club item in the target's hex and can be picked up by a 'Mech in that hex.
- [ ] Club attack uses Piloting −1, ⌈tonnage/5⌉ damage, standard hit-location table.
- [ ] Both-arm eligibility (undamaged shoulders + hands) and no-arm-weapons-fired rules enforced; carrying a club disables punch/push/physical-weapon attacks.
- [ ] Club is dropped on fall, carrying-arm destruction, or death, and becomes available in that hex again.

---

## Open Questions (for refinement)

1. **Skip declaration command** — is a silent skip acceptable (turn order advances with no broadcast), or should clients receive an explicit skip command for log/UI clarity? Weapons attacks currently send no explicit skip command — likely reuse that behavior.
2. **Punch geometry** — classic rules allow punching any adjacent hex in the front/side arc; confirm whether side-hex punches use the side hit-location table via `attackDirection` (they do per `GetHitLocation`).
3. **DFA miss target displacement** — the target "chooses" the adjacent passable hex. For human players this needs a selection interaction during resolution; propose nearest-valid auto-selection in v1 with the manual choice as a follow-up issue.
4. **Torso twist & punch arcs** — punches follow the attacker's *feet* orientation in classic rules; confirm the repo's stance on torso-twist-assisted punch arcs (MegaMek uses feet orientation; recommend matching).
5. **Physical weapon punch/kick table option (+4)** — ship in M4 or defer.
6. **Charge into empty hex** — a charge declared against an adjacent hex *without* the target staying (target moved away in a later declaration) must degrade to a normal move at resolution time; specify fallback behavior (recommend: treat as plain move, charge cancelled, no to-hit roll).
7. **Club carry duration** — classic rules govern club use within the turn of pickup; confirm whether a carried club persists across turns or is dropped at phase end (recommend: drop at phase end; the hex item persists for a later pickup).
