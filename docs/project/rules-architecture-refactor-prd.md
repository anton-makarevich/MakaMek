# Rules Architecture Refactor — Product Requirements Document

**Date:** 2026-10-18
**Status:** Draft — decision aid
**Related docs:** [`Rule-Related-PRs.md`](../analysis/Rule-Related-PRs.md) · [`Rules-Provider-Analysis.md`](../analysis/Rules-Provider-Analysis.md) · [`Rules-Provider-Analysis-Updated.md`](../analysis/Rules-Provider-Analysis-Updated.md)
**Related issues:** #1105 (unify modifier values), #1028 (movement cost via rules provider)
**Related PRs:** #1443 · #1450 · #1445 · #1451

---

## Executive Summary

This PRD defines the target architecture for how MakaMek sources, resolves, and applies game-rule values. It resolves the four open "rules provider" PRs into a single coherent design, grounded in established software-engineering principles (interface segregation, dependency inversion, information hiding) and in the actual call graph on `main`.

The core thesis: **entities are plain data, rules are resolved by services that already own the provider (or resolved once into unit state when the server applies a state change), and the provider is segregated by domain and composed — not inherited — into a rule set.**

---

## 1. Problem Statement

### 1.1 Current inconsistency

The codebase expresses the same class of information — a game-rule number — in several incompatible ways:

| Mechanism | Where | Example |
|---|---|---|
| **Hardcoded on the entity** | `src/MakaMek.Map/Models/Terrains/` — `abstract int MovementCost`, `Height`, `InterveningFactor` on every terrain subclass | `LightWoodsTerrain.MovementCost => 1` (additional cost on top of the 1 MP hex-entry cost) |
| **Hardcoded on modifier records** | `ProneAttackerModifier.DefaultValue`, `SkiddingAttackerModifier.DefaultValue`, `SkiddingTargetModifier.DefaultValue`, `PartialCoverModifier.DefaultValue` | `ProneAttackerModifier.DefaultValue = 2` |
| **Hardcoded inline in domain logic** | `Mech.MovementHeatPenalty`, `Mech.AttackHeatPenalty`, `Mech.ApplyHeatEffects` (life-support pilot damage), `Mech.GetMovementPoints` (run ×1.5), sensor/actuator/leg/hip penalties | `if (CurrentHeat >= 15) hits = 1;` |
| **Centralized in `IRulesProvider`** | `TotalWarfareRulesProvider` — 26 methods covering to-hit, hit location, cluster, heat, PSR, thresholds | `GetProneFiringModifier() => 2` |
| **Provider passed as a method parameter on the unit API** | `IUnit.GetHeatData(IRulesProvider)`, `IUnit.GetProjectedHeatValue(IRulesProvider)` | `unit.GetHeatData(Game.RulesProvider)` |

Concrete symptom (#1105): `IRulesProvider.GetProneFiringModifier()` exists but has **zero callers** — `Mech.GetAttackModifiers` reads `ProneAttackerModifier.DefaultValue` instead. A house-rule set that changes the prone modifier silently does nothing. Likewise, terrain movement cost cannot be changed without editing terrain classes (#1028).

A second, quieter symptom: `GetTerrainToHitModifier(MakaMekTerrains)` is a provider-side table with a `_ => 0` fallback. A newly added terrain silently gets a 0 to-hit modifier — correct-looking, possibly wrong.

### 1.2 What the four PRs do

| PR | Scope | Approach | Key weakness |
|---|---|---|---|
| **#1443** | Attack/to-hit modifiers only | Threads `IRulesProvider?` (optional) through `GetAttackModifiers`, `AttackScenario`, `ToHitCalculator`, bots | Optional-nullable provider → branching, "silently does nothing" paths, latent NRE |
| **#1450** | Heat, skid, falling, life-support + all of #1443 | Provider threaded into `GetMovementPoints`, `CanStandup`, `UnitExtensions`, `MovementState`, bots, MCP | Largest ripple; adds parameterless "legacy wrappers" (3rd source of truth); commits `BUG-INVENTORY.local.md` |
| **#1445** | Terrain MP cost via provider | Introduces `IMovementCostProvider` in Map; keeps `Terrain.MovementCost` as default | Duplicates the cost table in both Map and Core; `IRulesProvider : IMovementCostProvider` inverts the dependency |
| **#1451** | Terrain MP cost via provider (full) | Deletes `Terrain.MovementCost` entirely; adds path-cache invalidation | Largest blast radius (37 files); deletes the entity default the hybrid relies on; still duplicates table |

### 1.3 Overlaps

- **#1443 is a subset of #1450** — landing both creates duplicate/conflicting definitions.
- **#1445 and #1451 are mutually exclusive** — both touch the identical seam.
- All four bump `Directory.Build.props` (version collision).

---

## 2. Design Principles

Every architectural decision in this PRD is justified by one or more of these principles.

### 2.1 Deep Modules

> *"The best modules are those that provide powerful functionality yet have simple interfaces."* — John Ousterhout, *A Philosophy of Software Design*

**Application:** The caller (`ToHitCalculator`, pathfinding, `MechFactory`) makes one simple call — `_rules.GetProneFiringModifier()`, `_movementCost.GetMovementCost(terrain)`. It never branches on nullability, never checks "is there an override?", never knows which rule set is active.

**Caveat (same source):** Ousterhout flags *pass-through methods* and *shallow classes* as red flags. Interface segregation must not degenerate into many tiny interfaces and hand-written forwarding layers. Hence: a small number of domains (five), no empty interfaces, and forwarding written **once** (§3.4), not once per house rule.

### 2.2 Information Hiding

> *"Hide implementation details. Don't leak them across module boundaries."*

**Application:** Terrain classes expose `Height`, `InterveningFactor`, and a **default** `MovementCost` as intrinsic data. Callers never read `terrain.MovementCost` directly for gameplay; they ask `IMovementCostProvider`, which hides whether the value is the entity default or a rule-set override. The entity stays a POCO; the service hides the two-source merge.

### 2.3 Interface Segregation Principle (ISP)

> *"Clients should not be forced to depend on methods they do not use."* — SOLID

**Application:** `IRulesProvider` has 26 methods, but every consumer uses a small, disjoint subset (verified on `main`):

| Consumer | Methods used |
|---|---|
| `ToHitCalculator` | attacker/target movement, range, terrain to-hit, aimed shot, secondary target, partial cover |
| `WeaponAttackResolver`, `AttackerPartialCoverGate`, `WeaponsAttackState` | hit location, cluster hits, aimed-shot success values, partial cover |
| `PilotingSkillCalculator`, `FallProcessor`, `FallingDamageCalculator` | PSR modifier, requires-PSR, heavy-damage threshold, water depth, skid, facing/direction after fall, hit location |
| `HeatEffectsCalculator`, `Unit.GetHeatData`, `HeatProjectionViewModel` | shutdown, ammo explosion, movement heat, external heat cap, water dissipation cap |
| `MechFactory` | `GetStructureValues` only |

Split `IRulesProvider` along exactly these lines (§3.4).

### 2.4 Open-Closed Principle (OCP)

> *"Software entities should be open for extension, but closed for modification."* — SOLID

**Application:** Adding a new terrain type (`SwampTerrain`) must not require modifying any rules provider. The hybrid model (`providerOverride ?? entityDefault`) achieves this: a new terrain carries its own default, existing providers have no override for it, so the default stands.

**Known limit:** adding a terrain still touches the `MakaMekTerrains` enum, so Core/Map is never fully closed (noted in `Rules-Provider-Analysis-Updated.md`).

### 2.5 Composition across domains, inheritance within a domain

> *"Favor composition over class inheritance."* — GoF, *Design Patterns*

**Application, two levels:**

- **Across domains — composition.** A rule set is a composition of independent domain implementations (`CompositeRulesProvider(toHit, damage, piloting, heat)`). `IRulesProvider` must **not** inherit Map's `IMovementCostProvider` (the #1445 approach — "the whole Core rules surface becomes a subtype of a Map concern").
- **Within a domain — inheritance with virtual methods.** Overriding a single heat threshold should be one `override`, not a re-implementation or decorator of all of `IHeatRules`. `TotalWarfareHeatRules` exposes virtual methods; `MyHouseHeatRules : TotalWarfareHeatRules` overrides one.

This is the shape MegaMek actually uses (§4): `RulesManager` exposes domain accessors (composition), and `TWRulesHeat extends RulesHeat` (inheritance within a domain). The GoF maxim argues against deep hierarchies that couple unrelated concerns, not against a one-level, single-domain override.

### 2.6 Single Responsibility Principle (SRP)

> *"A class should have one, and only one, reason to change."* — SOLID

**Application:** `Mech` should not own heat→movement tables, life-support damage thresholds, or prone/skidding modifier values — those are rules. `Mech` should change only when unit *state* changes; the rules change only when the rule set changes. Where a unit genuinely needs a rule-derived value on its own API (e.g. heat-reduced MP), the value is **resolved by a service and stored as state** (§3.3.2), not computed from a rule table inside the entity.

### 2.7 Dependency Inversion Principle (DIP)

> *"Depend on abstractions, not concretions."* — SOLID

**Application:** `IMovementCostProvider` and its default implementation live in `MakaMek.Map` (Map has no project references; Core references Map). Map code depends only on the abstraction; any house-rule implementation can live in Core (or elsewhere) and is supplied through `IBattleMapFactory` by DI. Map never references a Core type.

---

## 3. Target Architecture

### 3.1 The data / rules / state taxonomy

The foundational distinction — *what varies together, and along which axis*:

| Category | Definition | Where it lives | Examples |
|---|---|---|---|
| **Game data** | Intrinsic description of a *thing*. Varies per-instance, scales with content, is authored (MTF, map files). | Entity properties, data files | Terrain height, water depth, intervening factor; weapon damage/heat/ranges; unit tonnage, base movement, armor layout |
| **Rules** | Procedures and tables of the *rule set*. Bounded in count, varies when you switch Total Warfare → house rules. | Segregated rules domains (§3.4) | Hit-location tables, cluster hits, structure-by-tonnage, heat scale, PSR modifiers, terrain to-hit, thresholds, movement-cost overrides |
| **State** | Current transient condition of an entity — including rule-derived values **resolved by the server** at the moment state changes. | On the entity | Current heat, resolved heat penalties, MP spent, damage, prone/skidding flags |

This taxonomy is the **acceptance test** for every future "should this be a rule?" question.

### 3.2 Entity resolution: the hybrid model (override + default)

`Terrain.MovementCost` is the **additional** MP cost of entering the terrain, on top of the fixed `HexEnterMovementCost { Value = 1 }` added by `Hex.GetEnterMovementCost` (Clear = 0, LightWoods = 1, HeavyWoods = 2, Water = 0/1/3 by depth). It stays on the entity as the default.

```csharp
// MakaMek.Map — entity stays a POCO, carries the default. No provider reference.
public abstract class Terrain
{
    public abstract MakaMekTerrains Id { get; }
    public abstract int Height { get; }            // data — intrinsic
    public abstract int InterveningFactor { get; } // data — intrinsic
    public abstract int MovementCost { get; }      // data — default additional MP cost, overridable by rules
}

// MakaMek.Map — the seam. Receives the Terrain instance (not just its Id),
// so instance-dependent defaults such as water depth remain expressible.
public interface IMovementCostProvider
{
    int GetMovementCost(Terrain terrain);
}

// MakaMek.Map — the default IS the Total Warfare rule set, because TW values
// are exactly the entity defaults. No table, no duplication.
public sealed class DefaultMovementCostProvider : IMovementCostProvider
{
    public static readonly DefaultMovementCostProvider Instance = new();
    public int GetMovementCost(Terrain terrain) => terrain.MovementCost;
}

// Anywhere (typically Core) — a house rule lists ONLY what it changes.
public sealed class HouseRuleMovementCostProvider(IReadOnlyDictionary<MakaMekTerrains, int> overrides)
    : IMovementCostProvider
{
    public int GetMovementCost(Terrain terrain)
        => overrides.TryGetValue(terrain.Id, out var cost) ? cost : terrain.MovementCost;
}
```

**Single source of truth:** the TW cost table exists once — in the terrain classes. House-rule providers contain only deltas.

**Why this is deep:** `Hex.GetEnterMovementCost` (both the normal and road-to-road branches) calls `_movementCostProvider.GetMovementCost(terrain)` — one call, no branching, no null check.

**Why this is open-closed:** a new `SwampTerrain` with `MovementCost => 2` works with every existing provider. Unknown terrains fall through to the default — no `ArgumentOutOfRangeException`.

**Wiring:** the provider is supplied once, at map creation, through `IBattleMapFactory` (`BattleMapFactory` is already the only creation seam — used by `ClientGame`, `JoinGameViewModel`, `MapConfigViewModel`). `BattleMap` passes it to its hexes. Because the provider is fixed for the lifetime of a map instance, the path caches (`_movementPathCache`, `_movementLongPathCache`) can never go stale — **no cache invalidation is needed** (consistent with the §9 non-goal of runtime rule switching).

**Same treatment for terrain to-hit (follow-up):** `GetTerrainToHitModifier` currently has the inverse shape (provider table, `_ => 0`). Either give `Terrain` a default to-hit modifier and apply the same hybrid, or explicitly document that unknown terrains have no to-hit modifier. This PRD recommends the hybrid for consistency, but defers it (§6, Phase 3 scope inventory).

### 3.3 Runtime per-unit modifiers

These values are **rules** (heat penalties, prone/skidding modifiers, falling-levels modifier, life-support pilot damage). Two resolution strategies apply, chosen by *who consumes the value*.

#### 3.3.1 Attack modifiers — resolve in the mechanics service

Every attack-modifier consumer goes through `AttackScenario` → `ToHitCalculator`, and `ToHitCalculator` already resolves the skidding-target and partial-cover modifiers itself. Prone and skidding-attacker follow the same precedent:

```csharp
// AttackScenario carries the facts (state), not the values (rules).
public record AttackScenario
{
    // ...
    public bool AttackerIsProne { get; init; }
    public bool AttackerIsSkidding { get; init; }
}

// ToHitCalculator (already holds the rules) resolves the values.
private IReadOnlyList<RollModifier> GetDetailedOtherModifiers(AttackScenario scenario)
{
    List<RollModifier> modifiers = [.. scenario.AttackerModifiers]; // component-state modifiers (sensors, actuators)

    if (scenario.AttackerIsProne)
        modifiers.Add(new ProneAttackerModifier { Value = _rules.GetProneFiringModifier() });
    if (scenario.AttackerIsSkidding)
        modifiers.Add(new SkiddingAttackerModifier { Value = _rules.GetSkiddingAttackerModifier() });
    if (scenario.TargetUnit?.IsSkidding == true)
        modifiers.Add(new SkiddingTargetModifier { Value = _rules.GetSkiddingTargetModifier() });
    // ...
}
```

`AttackScenario.FromUnits` and `FromHypothetical` (bots) populate the two flags. `Mech.GetAttackModifiers` stops emitting prone/skidding modifiers. No signature on `IUnit`/`Unit`/`Mech` changes.

#### 3.3.2 Heat-derived values — resolve when heat is applied, store as state

Heat→movement penalty feeds `GetMovementPoints`, which feeds `AvailableWalkingPoints`, `CanStandup`, `CanJump`, the UI-bound `MovementModifiers`, ~13 call sites in `MovementState`, the bot `MovementEngine`, and MCP `MovementTools`. No single service owns all of those, so "resolve in the service" does **not** work here without threading the provider through the unit API (the #1450 ripple).

But `CurrentHeat` changes in exactly one place: `Unit.ApplyHeat(HeatData)`, invoked from `HeatUpdatedCommand` on both server and client. `HeatPhase` (server) already holds the rules. Therefore:

```csharp
// HeatPhase (server) resolves rule-derived effects with IHeatRules and broadcasts them.
var heatData = _heatCalculator.GetHeatData(unit);          // service, see §3.3.3
var newHeat  = heatData.ResultingHeat(unit.CurrentHeat);
heatData = heatData with
{
    MovementPenalty   = _heatRules.GetHeatMovementPenalty(newHeat),
    AttackPenalty     = _heatRules.GetHeatAttackPenalty(newHeat),
    LifeSupportDamage = unit.HasDestroyedLifeSupport ? _heatRules.GetLifeSupportPilotDamage(newHeat) : 0
};

// Unit just stores what the server resolved — state, not rules.
public void ApplyHeat(HeatData heatData)
{
    CurrentHeat = ...;
    MovementHeatPenalty = heatData.MovementPenalty;  // was computed from a table in Mech
    AttackHeatPenalty   = heatData.AttackPenalty;
    ApplyLifeSupportDamage(heatData.LifeSupportDamage);
    ...
}
```

This matches the documented architecture ("mechanics are performed server-side and broadcast for client display") and continues an existing migration — `Mech.ApplyHeatEffects` already notes that heat shutdown moved to `HeatEffectsCalculator`. `GetMovementPoints` and every caller keep their signatures.

**Fallback (if the state approach is rejected):** `MechFactory` supplies the unit with a narrow `IHeatRules` at construction. This is an explicit, documented exception to "entities hold no rules", limited to one narrow interface — never the full `IRulesProvider`, never a per-call parameter.

#### 3.3.3 Existing provider-as-parameter on the unit API

`IUnit.GetHeatData(IRulesProvider)` and `IUnit.GetProjectedHeatValue(IRulesProvider)` already push a rules service through the unit API. All three callers already hold a provider (`HeatPhase`, `HeatProjectionViewModel`, bot `WeaponsEngine`). Move both into an `IHeatCalculator` service in `Models/Game/Mechanics` that depends on `IHeatRules`; the unit exposes only the raw state it needs (movement taken, declared weapon targets, heat sinks, external heat).

### 3.4 Interface segregation: domains composed into a rule set

```csharp
// Domain interfaces — mapped from actual consumers (§2.3).
public interface IToHitRules
{
    int GetAttackerMovementModifier(MovementType movementType);
    int GetTargetMovementModifier(int hexesMoved);
    int GetRangeModifier(RangeBracket rangeBracket, int rangeValue, int distance);
    int GetTerrainToHitModifier(MakaMekTerrains terrainType);
    int GetAimedShotModifier(PartLocation targetLocation);
    int[] GetAimedShotSuccessValues();
    int GetSecondaryTargetModifier(bool isFrontArc);
    int GetProneFiringModifier();
    int GetPartialCoverModifier();
    bool HasPartialCover(IUnit targetUnit, LineOfSightResult losResult);
    bool CanPartBeCovered(PartLocation location);
    // Phase 2: GetSkiddingAttackerModifier, GetSkiddingTargetModifier
}

public interface IDamageRules
{
    PartLocation GetHitLocation(int diceResult, HitDirection attackDirection);
    int GetClusterHits(int diceResult, int weaponSize);
}

public interface IPilotingRules
{
    int GetPilotingSkillRollModifier(PilotingSkillRollType psrType);
    bool RequiresPilotingSkillRoll(PilotingSkillRollType rollType);
    int GetHeavyDamageThreshold();
    int GetWaterDepthModifier(int waterDepth);
    int GetSkidModifier(int hexesMoved);
    HexDirection GetFacingAfterFall(int roll, HexDirection currentFacing);
    HitDirection GetAttackDirectionAfterFall(int roll);
    // Phase 4: GetFallingLevelsModifier
}

public interface IHeatRules
{
    int GetMovementHeatPoints(MovementType movementType, int movementPointSpent);
    int GetHeatShutdownAvoidNumber(int heatLevel);
    int GetHeatAmmoExplosionAvoidNumber(int heatLevel);
    int GetExternalHeatCap();
    int GetWaterHeatDissipationCap();
    // Phase 4: GetHeatMovementPenalty, GetHeatAttackPenalty, GetLifeSupportPilotDamage
}

// Construction-only. Injected directly into MechFactory; NOT reachable from IRulesProvider.
public interface IUnitConstructionRules
{
    Dictionary<PartLocation, int> GetStructureValues(int tonnage);
}
```

There is **no `IMovementRules`** in Phase 1: on `main` no method belongs to it (movement heat is a heat rule). Terrain movement cost lives behind Map's `IMovementCostProvider` (§3.2). An empty interface is pure overhead; add one when a real movement rule appears.

**Implementations — virtual methods per domain:**

```csharp
public class TotalWarfareHeatRules : IHeatRules
{
    public virtual int GetHeatShutdownAvoidNumber(int heatLevel) => heatLevel switch { /* TW table */ };
    // ...
}
```

**One composite, written once:**

```csharp
// Transitional aggregate: keeps existing IRulesProvider consumers and tests compiling.
public interface IRulesProvider : IToHitRules, IDamageRules, IPilotingRules, IHeatRules { }

// The ONLY class that forwards. House rules never write forwarding code.
public sealed class CompositeRulesProvider(
    IToHitRules toHit, IDamageRules damage, IPilotingRules piloting, IHeatRules heat) : IRulesProvider
{
    public int GetProneFiringModifier() => toHit.GetProneFiringModifier();
    public int GetHeatShutdownAvoidNumber(int heatLevel) => heat.GetHeatShutdownAvoidNumber(heatLevel);
    // ... one line per method
}
```

`TotalWarfareRulesProvider` becomes `new CompositeRulesProvider(new TotalWarfareToHitRules(), new TotalWarfareDamageRules(), new TotalWarfarePilotingRules(), new TotalWarfareHeatRules())`.

**Why keep the inheriting `IRulesProvider` at all:** 72 test files (127 occurrences) use `Substitute.For<IRulesProvider>()` or `new TotalWarfareRulesProvider()`. Because `IRulesProvider` *is-a* `IToHitRules`, consumers can narrow their constructor parameter types (`ToHitCalculator(IToHitRules)`) while existing tests keep compiling. The aggregate is a migration aid, not the extension point — the extension point is the domain classes plus `CompositeRulesProvider`. Once all consumers depend on domain interfaces, `IRulesProvider` may remain only where a holder genuinely needs the whole rule set (`BaseGame.RulesProvider`).

**DI registration:** register each domain interface and `IUnitConstructionRules` individually (`CoreServices.cs`, `BotContainerServices.cs`), plus `IRulesProvider` → `CompositeRulesProvider` built from the same instances.

### 3.5 House-rule composition

```csharp
// Override ONE heat rule — one class, one method.
public sealed class HarshHeatRules : TotalWarfareHeatRules
{
    public override int GetHeatShutdownAvoidNumber(int heatLevel)
        => heatLevel >= 28 ? 13 : base.GetHeatShutdownAvoidNumber(heatLevel);
}

// Compose the rule set — no forwarding, no reimplementation of other domains.
var houseRules = new CompositeRulesProvider(
    toHit:    new TotalWarfareToHitRules(),
    damage:   new TotalWarfareDamageRules(),
    piloting: new TotalWarfarePilotingRules(),
    heat:     new HarshHeatRules());

var houseMovementCost = new HouseRuleMovementCostProvider(
    new Dictionary<MakaMekTerrains, int> { [MakaMekTerrains.HeavyWoods] = 3 });
```

### 3.6 Rule-set consistency across peers

The provider is a per-process DI singleton, registered separately in the desktop/mobile app and in the bot container. Movement costs are computed on the client and sent in `PathSegmentData.Costs`; the server derives `MovementPointsSpent` from `MovementTaken.TotalCost` and does not recompute path costs. If host, clients, and bot containers ran different rule sets, the mismatch would be accepted silently.

**Requirement for this PRD (minimum):** document the invariant *"all peers and bot containers in a game run an identical rule set"* beside `IRulesProvider`.

**Future requirement (when the first non-TW rule set ships):** a rule-set identifier is part of game setup, broadcast in the lobby, and validated on join.

---

## 4. MegaMek Comparison

MegaMek's rules architecture (`megamek/src/megamek/common/rules/`) was introduced on **2026-06-27** (commit `1b54f67fc1`, "Changing rules system to use RulesManager"). It is a recent refactor of a mature codebase — useful as a design reference, **not** as decades of proof.

| MegaMek concept | MakaMek equivalent | Notes |
|---|---|---|
| `RulesManager` interface with **20 accessors** (`getRulesHeat()`, `getRulesTarget()`, …) | `CompositeRulesProvider` (4 runtime domains) + `IUnitConstructionRules` | MegaMek **composes** domains via accessors; it does not inherit them into one interface |
| Abstract `RulesHeat`, `RulesTarget`, … | `IHeatRules`, `IToHitRules`, … | Per-domain contract |
| `TWRulesHeat extends RulesHeat`, `CoreRules*`, `TacOpsScanning` | `TotalWarfareHeatRules` (virtual) / `HarshHeatRules : TotalWarfareHeatRules` | **Inheritance within a domain** for partial override |
| `TWRulesManager` / `CoreRulesManager` | `CompositeRulesProvider` instances | Rule-set selection = choosing which domain instances to compose |
| `GameType`, `GameOptions` (~200+), `Era`, `TechConstants`, faction RAT | Not needed | MakaMek targets Classic BattleTech, no option-driven or era-driven rules |

**Key takeaway:** the transferable pattern is *composition across domains, inheritance within a domain* — exactly §2.5. The flat `IRulesProvider : IToHitRules, IHeatRules, …` inheritance is kept only as a transitional aggregate (§3.4), not as the model MegaMek validates.

---

## 5. Disposition of the Four PRs

### 5.1 Attack + unit modifiers (#1443 / #1450)

**Decision: close both, implement the refactor directly.**

- Both introduce the optional `IRulesProvider? = null` pattern (branching, silent no-op, latent NRE).
- #1450 adds parameterless legacy wrappers (a third source of truth) and commits `BUG-INVENTORY.local.md`.
- Both thread the provider through `Mech`/`Unit`/`IUnit` method signatures — the ripple §3.3 avoids.

Attack modifiers are resolved in `ToHitCalculator` (§3.3.1); heat-derived values become server-resolved state (§3.3.2).

### 5.2 Terrain movement cost (#1445 / #1451)

**Decision: close both, implement the hybrid directly.**

- #1445's `IMovementCostProvider` seam in Map is correct; its `IRulesProvider : IMovementCostProvider` inheritance is not. Its `DefaultMovementCostProvider` should return `terrain.MovementCost` instead of re-listing terrains.
- #1451's deletion of `Terrain.MovementCost` removes the entity default the hybrid relies on; its cache invalidation is unnecessary once the provider is fixed per map instance.
- Both duplicate the cost table. In the target design the table exists once — on the terrain classes.

### 5.3 Interface segregation

**Decision: implement first.** It is independent of the other work, behavior-neutral, and lets later phases depend on narrow interfaces.

---

## 6. Implementation Phases

Each phase is an independently mergeable PR with its own version bump.

### Phase 1 — Interface segregation (no behavior change)

1. Create `IToHitRules`, `IDamageRules`, `IPilotingRules`, `IHeatRules`, `IUnitConstructionRules` in `src/MakaMek.Core/Models/Game/Rules/` with the method mapping from §3.4.
2. Create `TotalWarfareToHitRules`, `TotalWarfareDamageRules`, `TotalWarfarePilotingRules`, `TotalWarfareHeatRules`, `TotalWarfareUnitConstructionRules` with **virtual** methods; move the bodies from `TotalWarfareRulesProvider`.
3. Create `CompositeRulesProvider`; make `IRulesProvider` the aggregate of the four runtime domains; remove `GetStructureValues` from it.
4. Inject `IUnitConstructionRules` directly into `MechFactory`.
5. Narrow mechanics-service constructors to the domain interface they use (`ToHitCalculator(IToHitRules)`, `HeatEffectsCalculator(IHeatRules, …)`, etc.).
6. Update DI registration in `CoreServices.cs` and `BotContainerServices.cs`.

**Exit criteria:** all existing tests pass; no behavior change; `MechFactory` has no `IRulesProvider` dependency.

### Phase 2 — Attack-modifier quick win (#1105)

1. Add `GetSkiddingAttackerModifier()` / `GetSkiddingTargetModifier()` to `IToHitRules`.
2. Add `AttackerIsProne` / `AttackerIsSkidding` to `AttackScenario`; populate in `FromUnits` and `FromHypothetical`.
3. `ToHitCalculator` adds prone, skidding-attacker, and skidding-target modifiers from the rules (§3.3.1).
4. Remove prone/skidding emission from `Mech.GetAttackModifiers`.
5. Delete the four `DefaultValue` consts (`PartialCoverModifier.DefaultValue` is already unused).

**Exit criteria:** `GetProneFiringModifier` has a caller; no `DefaultValue` consts on modifier records; bot hit-probability unchanged under TW rules; `IUnit` signatures unchanged.

### Phase 3 — Terrain movement cost (#1028)

1. Keep `Terrain.MovementCost` as the default additional cost.
2. Add `IMovementCostProvider` and `DefaultMovementCostProvider` (returns `terrain.MovementCost`) to `src/MakaMek.Map/Models/`.
3. `IBattleMapFactory`/`BattleMapFactory` receive an `IMovementCostProvider` via DI (default `DefaultMovementCostProvider.Instance`) and pass it to `BattleMap`, which passes it to hex cost evaluation.
4. `Hex.GetEnterMovementCost` uses the provider in both the normal and road-to-road branches.
5. Decide terrain to-hit: apply the hybrid (`Terrain` default to-hit modifier) or document the `_ => 0` behavior.
6. Produce the **rule-value inventory** (below) and mark each row in-scope / deferred for Phase 4.

**Exit criteria:** standalone maps work unchanged; a custom provider injected via the factory changes pathfinding costs; unknown terrains fall back to the entity default; no Core type referenced from Map.

### Phase 4 — Heat-derived values as server-resolved state

1. Add `GetHeatMovementPenalty`, `GetHeatAttackPenalty`, `GetLifeSupportPilotDamage` to `IHeatRules`; add `GetFallingLevelsModifier` to `IPilotingRules`.
2. Introduce `IHeatCalculator` (depends on `IHeatRules`); move `GetHeatData` / `GetProjectedHeatValue` off `IUnit` (callers: `HeatPhase`, `HeatProjectionViewModel`, `WeaponsEngine`).
3. Extend `HeatData` with resolved `MovementPenalty`, `AttackPenalty`, `LifeSupportDamage`; `HeatPhase` resolves them on the server.
4. `Unit.ApplyHeat` stores the resolved penalties; remove the heat tables from `Mech.MovementHeatPenalty` / `AttackHeatPenalty` and the thresholds from `Mech.ApplyHeatEffects`.
5. Ensure save/restore and unit-state reconstruction carry the resolved penalties.

**Exit criteria:** no heat thresholds remain in `Mech`; no `IRulesProvider` parameters on `IUnit`; `GetMovementPoints` and all ~13 `MovementState` call sites unchanged.

### Phase 5 — Documentation and boundary lock

1. Add the §3.1 "Data vs Rules vs State" note beside the rules interfaces.
2. Document the `IMovementCostProvider` contract and the `override ?? entityDefault` model.
3. Document the rule-set consistency invariant (§3.6).
4. Add the §3.5 house-rule example to the docs.

### Rule-value inventory (to complete in Phase 3)

| Value | Current location | Category | Target phase |
|---|---|---|---|
| Prone / skidding attacker / skidding target modifiers | modifier `DefaultValue` consts | rule | Phase 2 |
| Terrain additional MP cost | `Terrain.MovementCost` | rule (entity default) | Phase 3 |
| Terrain to-hit modifier | `GetTerrainToHitModifier` table, `_ => 0` | rule | Phase 3 decision |
| Heat→movement / heat→attack penalty | `Mech.MovementHeatPenalty` / `AttackHeatPenalty` | rule | Phase 4 |
| Life-support pilot damage (15 / 26) | `Mech.ApplyHeatEffects` | rule | Phase 4 |
| Falling-levels modifier | piloting/fall code | rule | Phase 4 |
| Run MP multiplier (×1.5) | `Mech.GetMovementPoints` | rule | deferred |
| Sensor-hit modifier | `SensorHitModifier.Create` | rule | deferred |
| Arm/leg actuator, leg/hip-destroyed penalties | `Mech` / penalty records | rule | deferred |
| Stand-up cost | `Mech.EffectiveStandupCost` | rule | deferred |

---

## 7. Acceptance Criteria

| # | Criterion | Verifies |
|---|---|---|
| 1 | A new terrain type (`SwampTerrain`) works with every existing `IMovementCostProvider` without modifying any provider | Open-Closed |
| 2 | A house-rule set overrides a single rule (e.g. one heat threshold) with one subclass overriding one method, plus one `CompositeRulesProvider` construction | Composition / partial override |
| 3 | `MechFactory` depends only on `IUnitConstructionRules`; `GetStructureValues` is not reachable from `IRulesProvider` | Interface Segregation |
| 4 | Each mechanics service depends only on the domain interface(s) it uses | Interface Segregation |
| 5 | No `IUnit`/`Unit`/`Mech` member takes `IRulesProvider` (or a domain interface) as a parameter — including the existing `GetHeatData` / `GetProjectedHeatValue` | Information Hiding |
| 6 | `IRulesProvider.GetProneFiringModifier` is used by `ToHitCalculator`; no `DefaultValue` consts remain on modifier records | Single source of truth (#1105) |
| 7 | No heat thresholds or heat tables remain in `Mech` | SRP |
| 8 | Terrain `Height`, `InterveningFactor` remain entity data; TW terrain MP costs exist only on terrain classes | Data/Rules boundary, no duplication |
| 9 | `IMovementCostProvider` and `DefaultMovementCostProvider` live in Map; Map references no Core type | DIP |
| 10 | A custom `IMovementCostProvider` supplied via `IBattleMapFactory` affects pathfinding | Testability (#1028) |
| 11 | Adding a new rules domain does not modify existing domain interfaces or their implementations | Interface Segregation |
| 12 | All existing tests pass after each phase | Regression |

---

## 8. Risks and Mitigations

| Risk | Mitigation |
|---|---|
| Phase 4 changes the heat command payload and unit-state restore | Isolated phase; `HeatData` already flows through `HeatUpdatedCommand`; add round-trip serialization and restore tests |
| Forwarding boilerplate in `CompositeRulesProvider` | Written once; house rules never forward. Pass-through cost is accepted in one class only |
| Transitional `IRulesProvider` aggregate becomes permanent god interface | Phase 1 narrows all mechanics-service constructors; AC 4 enforces it; the aggregate keeps no members of its own |
| Peers running different rule sets accept each other's values silently | §3.6 invariant documented now; rule-set ID handshake required before the first non-TW rule set ships |
| Silent defaults for new content (`_ => 0` style fallbacks) | Prefer entity defaults authored with the entity (hybrid); Phase 3 decides terrain to-hit explicitly |
| Performance regression from indirection | One virtual call or dictionary lookup per value, in-memory, no I/O or reflection — negligible in a turn-based game |
| Breaking external consumers of `IRulesProvider` | The aggregate keeps all runtime methods; only `GetStructureValues` moves (single caller: `MechFactory`) |

---

## 9. Non-Goals

- **Runtime rule switching** (hot-swapping providers mid-game). The movement-cost provider is fixed per map instance; no cache invalidation is built.
- **Rule-set selection UI / lobby negotiation.** Only the invariant is documented (§3.6).
- **GameOptions system** (MegaMek's ~200+ options). House rules are code-level compositions.
- **Era/tech-level/faction rules.**
- **Mod support / scripting.**
- **Alpha Strike / BattleForce rules.**
- **Deferred inventory rows** (§6) — run multiplier, sensor/actuator penalties, stand-up cost.

---

## 10. Summary of Decisions

| Decision | Justified by |
|---|---|
| Entities are POCOs carrying defaults; services resolve rules | Information Hiding, Deep Modules |
| `override ?? entityDefault` for terrain cost; TW table lives only on terrain classes | Open-Closed, single source of truth |
| `IMovementCostProvider` + default (identity) provider in Map, injected via `IBattleMapFactory` | DIP (Map has no Core reference) |
| No path-cache invalidation — provider fixed per map | YAGNI, consistent with non-goals |
| Rules split into four runtime domains + construction; no empty `IMovementRules` | Interface Segregation, derived from actual consumers |
| Composition across domains (`CompositeRulesProvider`), virtual inheritance within a domain | Composition over Inheritance, MegaMek precedent, minimal house-rule code |
| `IRulesProvider` aggregate kept as a migration aid only | Test compatibility (72 files) while narrowing consumers |
| `IUnitConstructionRules` injected directly into `MechFactory` | Interface Segregation (construction-only concern) |
| Attack modifiers resolved in `ToHitCalculator` | Existing precedent (skidding-target, partial cover) |
| Heat-derived values resolved server-side in `HeatPhase`, stored as unit state | Server-authoritative architecture; avoids unit-API ripple |
| `GetHeatData` / `GetProjectedHeatValue` move to a heat service | Removes existing provider-as-parameter on `IUnit` |
| All peers run an identical rule set (documented invariant) | Client-computed costs are not re-validated by the server |
| Close all four PRs; implement the refactor directly | The PRs' plumbing is incompatible with the target architecture |

---

**Document Version:** 1.1 — corrected terrain cost semantics and prone value; replaced the duplicating movement-cost adapter with an identity default in Map; removed cache invalidation; split Phase 3 into attack (service) and heat (server-resolved state) strategies; added `GetHeatData` migration, concrete domain mapping, composite + virtual-domain house-rule model, rule-set consistency invariant, and rule-value inventory; corrected MegaMek comparison and quote attribution.
**Author:** AI-assisted architecture analysis
**Reviewers:** TBD
