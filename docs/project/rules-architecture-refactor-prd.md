# Rules Architecture Refactor — Product Requirements Document

**Date:** 2026-10-18
**Status:** Draft — decision aid
**Related docs:** [`Rule-Related-PRs.md`](../analysis/Rule-Related-PRs.md) · [`Rules-Provider-Analysis.md`](../analysis/Rules-Provider-Analysis.md) · [`Rules-Provider-Analysis-Updated.md`](../analysis/Rules-Provider-Analysis-Updated.md)
**Related issues:** #1105 (unify modifier values), #1028 (movement cost via rules provider)
**Related PRs:** #1443 · #1450 · #1445 · #1451

---

## Executive Summary

This PRD defines the target architecture for how MakaMek sources, resolves, and applies game-rule values. It resolves the four open "rules provider" PRs into a single coherent design, grounded in established software-engineering principles (interface segregation, composition over inheritance, open-closed, information hiding).

The core thesis: **entities are plain data, rules are resolved by services that already own the provider, and the provider interface is segregated by domain behind a facade.**

---

## 1. Problem Statement

### 1.1 Current inconsistency

The codebase expresses the same class of information — a game-rule number — in two incompatible ways:

| Mechanism | Where | Example |
|---|---|---|
| **Hardcoded on the entity** | `src/MakaMek.Map/Models/Terrains/` — `abstract int MovementCost`, `Height`, `InterveningFactor` on every terrain subclass | `ClearTerrain.MovementCost => 1` |
| **Hardcoded on modifier records** | `ProneAttackerModifier.DefaultValue`, `SkiddingAttackerModifier.DefaultValue`, … | `DefaultValue = 1` |
| **Hardcoded inline in domain logic** | `Mech.ApplyHeatEffects` (life-support pilot damage), `Mech.GetMovementPoints` (heat→movement penalty) | `if (heat >= 15) damage = 2;` |
| **Centralized in `IRulesProvider`** | `TotalWarfareRulesProvider` — ~26 methods covering to-hit, hit-location, cluster, heat, PSR, thresholds | `GetProneFiringModifier() => 1` |

The result: a house-rule set that changes, say, the prone-firing modifier silently does nothing on any call path that reads the const. Terrain movement cost cannot be changed without editing terrain classes.

### 1.2 What the four PRs do

| PR | Scope | Approach | Key weakness |
|---|---|---|---|
| **#1443** | Attack/to-hit modifiers only | Threads `IRulesProvider?` (optional) through `GetAttackModifiers`, `AttackScenario`, `ToHitCalculator`, bots | Optional-nullable provider → branching, "silently does nothing" paths, latent NRE |
| **#1450** | Heat, skid, falling, life-support + all of #1443 | Provider threaded into `GetMovementPoints`, `CanStandup`, `UnitExtensions`, `MovementState`, bots, MCP | Largest ripple (26 files); adds parameterless "legacy wrappers" (3rd source of truth); commits `BUG-INVENTORY.local.md` |
| **#1445** | Terrain MP cost via provider | Introduces `IMovementCostProvider` in Map; keeps `Terrain.MovementCost` as default | Duplicates the cost table in both Map and Core; `IRulesProvider : IMovementCostProvider` inverts the dependency |
| **#1451** | Terrain MP cost via provider (full) | Deletes `Terrain.MovementCost` entirely; adds path-cache invalidation | Largest blast radius (38 files); deletes the entity default the hybrid relies on; still duplicates table |

### 1.3 Overlaps

- **#1443 is a subset of #1450** — landing both creates duplicate/conflicting definitions.
- **#1445 and #1451 are mutually exclusive** — both touch the identical seam.
- All four bump `Directory.Build.props` (version collision).

---

## 2. Design Principles

Every architectural decision in this PRD is justified by one or more of these principles. Each principle is stated, then applied.

### 2.1 Deep Modules

> *"The best modules are those that provide powerful functionality yet have simple interfaces."* — Clean Code, Martin

A deep module hides a complex implementation behind a simple call. The caller should not need to know *which* rule set is active or *where* a value is stored.

**Application:** The caller (`ToHitCalculator`, pathfinding, `MechFactory`) calls `_rules.GetProneFiringModifier()` — a single, simple call. The provider internally decides whether to return a hardcoded Total Warfare value or a house-rule override. The caller never branches on nullability, never checks "is there an override?", never knows about terrain defaults.

### 2.2 Information Hiding

> *"Hide implementation details. Don't leak them across module boundaries."*

**Application:** Terrain classes expose `Height` and `InterveningFactor` as intrinsic data (they are properties of the physical terrain, not rules). They do **not** expose a `MovementCost` that callers must then "override" via a provider lookup — that leaks the rule-resolution concern into every call site. Instead, the *service* that needs the movement cost (`IMovementCostProvider`) owns the resolution logic: `override ?? entityDefault`. The entity stays a POCO; the service hides the two-source merge.

### 2.3 Interface Segregation Principle (ISP)

> *"Clients should not be forced to depend on methods they do not use."* — SOLID

**Application:** The current `IRulesProvider` is a ~30-method god interface. Every consumer — `ToHitCalculator`, `PilotingSkillCalculator`, `MechFactory`, `BaseGame` — depends on all 30 methods even if it uses only 2–3. This creates coupling: adding a construction-only method (`GetStructureValues`) forces every implementor to provide it. MegaMek's `RulesManager` (18 category interfaces: `RulesTarget`, `RulesHeat`, `RulesMovement`, `RulesPilot`, `RulesWeapons`, …) is the production-tested precedent.

**Application:** Split `IRulesProvider` into domain interfaces (`IToHitRules`, `IHeatRules`, `IPilotingRules`, `IMovementRules`, `IDamageRules`, `IUnitConstructionRules`) composed behind a facade (`IRulesProvider : IToHitRules, IHeatRules, …`). A house-rule set implements only the domains it overrides; the facade delegates to Total Warfare defaults for the rest.

### 2.4 Open-Closed Principle (OCP)

> *"Software entities should be open for extension, but closed for modification."* — SOLID

**Application:** Adding a new terrain type (`SwampTerrain`) must not require modifying any rules provider. The hybrid model (`providerOverride ?? entityDefault`) achieves this: a new terrain carries its own default movement cost, and existing providers return `null` for it (no override), so the default stands. The provider is *closed* for modification when new entities arrive.

### 2.5 Composition Over Inheritance

> *"Favor composition over class inheritance."* — GoF, Design Patterns

**Application:** `IRulesProvider` must **not** inherit `IMovementCostProvider` (the #1445 approach — "the whole Core rules surface becomes a subtype of a Map concern"). Instead, Core *supplies an adapter*: a small `TotalWarfareMovementCostProvider : IMovementCostProvider` that wraps the terrain-cost table. Map depends on `IMovementCostProvider` (abstraction); Core provides the implementation. The dependency direction is preserved: Map → abstraction, Core → implementation.

### 2.6 Single Responsibility Principle (SRP)

> *"A class should have one, and only one, reason to change."* — SOLID

**Application:** `Mech` should not compute heat→movement penalties, life-support pilot damage, or prone/skidding modifiers — those are rules, not unit state. Moving them to `IHeatRules` / `IToHitRules` means `Mech` changes only when unit *state* changes; the rules change only when the rule set changes.

### 2.7 Dependency Inversion Principle (DIP)

> *"Depend on abstractions, not concretions."* — SOLID

**Application:** `IMovementCostProvider` lives in `MakaMek.Map` (the consumer). `TotalWarfareMovementCostProvider` lives in `MakaMek.Core` (the implementation). Both depend on the abstraction. Neither depends on the other's concrete type.

---

## 3. Target Architecture

### 3.1 The data / rules / state taxonomy

The foundational distinction — *what varies together, and along which axis*:

| Category | Definition | Where it lives | Examples |
|---|---|---|---|
| **Game data** | Intrinsic description of a *thing*. Varies per-instance, scales with content, is authored (MTF, map files). | Entity properties, data files | Terrain height, water depth, intervening factor; weapon damage/heat/ranges; unit tonnage, base movement, armor layout |
| **Rules** | Procedures and tables of the *rule set*. Bounded in count, varies when you switch Total Warfare → house rules. | `IRulesProvider` (segregated interfaces) | Hit-location tables, cluster hits, structure-by-tonnage, heat scale, PSR modifiers, terrain to-hit, thresholds, movement-cost formulas |
| **State** | Current transient condition of an entity. | On the entity | Current heat, MP spent, damage, prone/skidding flags |

This taxonomy is the **acceptance test** for every future "should this be a rule?" question.

### 3.2 Entity resolution: the hybrid model (override + default)

```csharp
// Entity stays a POCO — just a default. No IRulesProvider reference.
public abstract class Terrain
{
    public abstract MakaMekTerrains Id { get; }
    public abstract int Height { get; }          // data — intrinsic
    public abstract int InterveningFactor { get; } // data — intrinsic
    public abstract int MovementCost { get; }      // data — default, overridable by rules
}

// The service that already owns the provider performs the keyed override.
// This is the ONLY place the two sources meet.
public interface IMovementCostProvider
{
    int GetMovementCost(Terrain terrain);
}

// Core supplies an adapter — composition, not inheritance.
// Map depends on the abstraction; Core provides the implementation.
public class TotalWarfareMovementCostProvider : IMovementCostProvider
{
    public int GetMovementCost(Terrain terrain) => terrain.Id switch
    {
        MakaMekTerrains.Clear => 1,
        MakaMekTerrains.LightWoods => 2,
        MakaMekTerrains.HeavyWoods => 3,
        // ...
        _ => terrain.MovementCost  // fallback to entity default — no throw
    };
}
```

**Why this is deep:** the caller (`Hex.GetEnterMovementCost`, pathfinding) calls `_movementCostProvider.GetMovementCost(terrain)` — one call, no branching, no null check.

**Why this hides information:** the caller doesn't know or care whether the value came from a table or the entity. The resolution is encapsulated.

**Why this is open-closed:** a new `SwampTerrain` with `MovementCost => 3` works with every existing provider. A house rule overrides only what it changes; everything else falls through to the default.

### 3.3 Runtime per-unit modifiers: resolve in mechanics services

Values like heat→movement penalty, heat→attack penalty, prone/skidding modifiers, falling-levels modifier, and life-support pilot damage are **rules**, not unit state. They are already assembled inside mechanics services that own the provider:

```csharp
// ToHitCalculator already receives IRulesProvider — it just uses more of it.
public class ToHitCalculator
{
    private readonly IToHitRules _rules;
    
    public int GetAttackModifiers(AttackScenario scenario)
    {
        var modifiers = new List<AttackModifier>();
        // ... range, movement, terrain ...
        if (scenario.Attacker.IsProne)
            modifiers.Add(new ProneAttackerModifier(_rules.GetProneFiringModifier()));
        if (scenario.Attacker.IsSkidding)
            modifiers.Add(new SkiddingAttackerModifier(_rules.GetSkiddingAttackerModifier()));
        return modifiers.Sum(m => m.Value);
    }
}
```

**Why this is correct:** the dependency lives where it already lives (the service that owns the provider). No `provider` parameter threads through `Mech.GetAttackModifiers` → `Unit` → `IUnit` → `MovementState` → bots → MCP. The ripple shrinks to zero because the API doesn't change.

### 3.4 Interface segregation: the facade pattern

```csharp
// Domain interfaces — each is a deep module with a simple interface.
public interface IToHitRules { /* ~8 methods: range, movement, terrain, aimed shot, secondary target, prone, skidding, partial cover */ }
public interface IHeatRules { /* ~6 methods: shutdown, ammo explosion, external cap, water dissipation, movement heat, heat penalties */ }
public interface IPilotingRules { /* ~6 methods: PSR modifiers, water depth, skid, fall facing, attack direction, structure-by-tonnage */ }
public interface IMovementRules { /* ~4 methods: attacker/target movement modifier, heavy damage threshold, movement heat */ }
public interface IDamageRules { /* ~4 methods: hit location, cluster hits, structure values, falling */ }
public interface IUnitConstructionRules { /* structure-by-tonnage only — used by MechFactory, never in hot path */ }

// Facade — composes the domains. This is what BaseGame holds.
public interface IRulesProvider : IToHitRules, IHeatRules, IPilotingRules, IMovementRules, IDamageRules
{
    IUnitConstructionRules Construction { get; }  // construction-only, accessed only by MechFactory
}
```

**Why this is segregated:** `MechFactory` depends only on `IUnitConstructionRules` (1 method). `ToHitCalculator` depends only on `IToHitRules` (~8 methods). Neither is forced to see the other's surface. Adding a new domain (e.g., `IWeaponRules`) doesn't touch existing implementors.

**Why this is a facade, not a god interface:** the facade adds no behavior — it only composes. Each domain interface is independently implementable, testable, and mixable.

### 3.5 House-rule composition

```csharp
// A house-rule set is just a different composition — no inheritance from Total Warfare.
public class CustomHouseRulesProvider : IRulesProvider
{
    private readonly IToHitRules _toHit;
    private readonly IHeatRules _heat;
    // ...
    
    public CustomHouseRulesProvider()
    {
        _toHit = new TotalWarfareToHitRules();           // inherit TW
        _heat = new CustomHeatRules(maxShutdown: 28);    // override heat
        _piloting = new TotalWarfarePilotingRules();    // inherit TW
        // ...
    }
    
    public IUnitConstructionRules Construction => new TotalWarfareUnitConstructionRules();
}
```

**Why this is composition over inheritance:** the house-rule set doesn't subclass `TotalWarfareRulesProvider` (which would force it to reimplement every domain). It composes domain implementations, overriding only what it changes.

---

## 4. MegaMek Comparison

MegaMek's production architecture (analyzed in `C:\Users\anton\myrepos\megamek\session-ses_eaff.md`) validates the facade + segregation approach at scale:

| MegaMek concept | MakaMek equivalent | Notes |
|---|---|---|
| `GameType` enum (TW/AS/BF/SBF) | Not needed yet | MakaMek targets Classic BattleTech only; the facade supports future expansion |
| `GameOptions` (~200+ options) | Not needed yet | MakaMek has no option-driven rules; house rules are code-level compositions |
| `RulesManager` (18 category interfaces) | `IRulesProvider` facade (5–6 domain interfaces) | Direct structural parallel — MegaMek proved this scales |
| `CoreRules*` / `TWRules*` implementations | `TotalWarfare*Rules` / `CustomHouseRules` | Same strategy-pattern selection |
| `Era` system | Not needed yet | MakaMek has no era-driven equipment gating |
| `TechConstants` matrix | Not needed yet | MakaMek has no tech-level validation |
| Faction-specific RAT | Not needed yet | MakaMek has no force generation |
| `Entity` type bitmask + per-type classes | `Mech` (single class) | MakaMek's unit model is simpler; the rules segregation is the transferable pattern |

**Key takeaway:** MegaMek's `RulesManager` is a facade over 18 segregated rule interfaces, each with CoreRules/TWRules implementations selected at runtime. This is exactly the pattern proposed here, scaled down to MakaMek's current domain complexity. The pattern has survived decades of BattleTech rules evolution in MegaMek — it will scale with MakaMek.

---

## 5. Disposition of the Four PRs

### 5.1 Attack + unit modifiers (#1443 / #1450)

**Decision: close both, implement the refactor directly.**

Neither PR should be merged as-is because:
- Both introduce the optional `IRulesProvider? = null` pattern (branching, silent no-op, latent NRE).
- #1450 adds parameterless legacy wrappers (a third source of truth).
- Both thread the provider through `Mech`/`Unit`/`IUnit` method signatures — the exact ripple the refactor eliminates.

The refactor (§3.3) achieves the same goal (all modifiers rule-driven) without the plumbing damage, by resolving modifiers inside the mechanics services that already own the provider.

### 5.2 Terrain movement cost (#1445 / #1451)

**Decision: close both, implement the hybrid adapter directly.**

- #1445's `IMovementCostProvider` seam is correct, but its `IRulesProvider : IMovementCostProvider` inheritance inverts the dependency. Replace with a `TotalWarfareMovementCostProvider` adapter (§3.2).
- #1451's deletion of `Terrain.MovementCost` removes the entity default the hybrid relies on. Restore the default property and use the `override ?? entityDefault` model.
- Both duplicate the cost table. The adapter is the single source.

### 5.3 Interface segregation

**Decision: implement as prerequisite.**

The facade split (§3.4) is independent of the modifier/movement-cost work and unblocks it. Do it first.

---

## 6. Implementation Phases

### Phase 1 — Interface segregation (no behavior change)

**Goal:** split `IRulesProvider` into domain interfaces + facade. Purely mechanical — no logic changes.

1. Create `IToHitRules`, `IHeatRules`, `IPilotingRules`, `IMovementRules`, `IDamageRules`, `IUnitConstructionRules` in `src/MakaMek.Core/Models/Game/Rules/`.
2. Create `TotalWarfareToHitRules`, `TotalWarfareHeatRules`, etc. — move existing `TotalWarfareRulesProvider` methods into the appropriate domain class.
3. Make `TotalWarfareRulesProvider` a thin facade implementing `IRulesProvider` by delegating to the domain instances.
4. Update `BaseGame.RulesProvider` consumers to use the facade (no signature changes).
5. Move `GetStructureValues` access behind `IRulesProvider.Construction`.

**Exit criteria:** all existing tests pass; `IRulesProvider` facade is in place; no behavior change.

### Phase 2 — Terrain movement cost via adapter

**Goal:** terrain cost resolves through `IMovementCostProvider` with entity-default fallback.

1. Keep `Terrain.MovementCost` as the entity default (restore if deleted).
2. Add `IMovementCostProvider` to `src/MakaMek.Map/Models/`.
3. Add `TotalWarfareMovementCostProvider` to `src/MakaMek.Core/` — the adapter with the cost table + fallback.
4. Wire `BattleMap` to accept an `IMovementCostProvider` (default to `TotalWarfareMovementCostProvider` if none supplied).
5. Update `Hex.GetEnterMovementCost` to call the provider instead of reading `terrain.MovementCost` directly.
6. Add path-cache invalidation when the provider's rules change (the #1451 insight).

**Exit criteria:** standalone maps work with default costs; a custom provider can override any terrain; unknown terrains fall back to default; cache invalidates on rule change.

### Phase 3 — Runtime modifiers resolved in services

**Goal:** heat/prone/skidding/falling/life-support values resolve inside mechanics services, not on unit APIs.

1. Add `GetHeatMovementPenalty`, `GetHeatAttackPenalty`, `GetLifeSupportPilotDamage`, `GetFallingLevelsModifier`, `GetSkiddingAttackerModifier`, `GetSkiddingTargetModifier` to `IHeatRules` / `IToHitRules` / `IPilotingRules`.
2. Move the inline computations from `Mech.GetMovementPoints`, `Mech.ApplyHeatEffects`, `CanStandup`, `CanChangeFacingWhileProne` into the appropriate rules domain.
3. Remove `DefaultValue` consts from `ProneAttackerModifier`, `SkiddingAttackerModifier`, `SkiddingTargetModifier`, `PartialCoverModifier` — the modifier records become pure data (location + value), and the value comes from the rules service.
4. Update `ToHitCalculator` and `PilotingSkillCalculator` to call the rules services.
5. Remove the provider parameter from `Mech.GetAttackModifiers` / `GetMovementPoints` — the services handle it.
6. Update bots, MCP tools, `MovementState`, `UnitExtensions` to read from the services (or from pre-resolved values on the unit state).

**Exit criteria:** no `DefaultValue` consts remain on modifier records; no `IRulesProvider?` optional parameters; `Mech`/`Unit`/`IUnit` signatures unchanged; all modifier values resolve through the rules facade.

### Phase 4 — Documentation and boundary lock

1. Add a "Data vs Rules vs State" note beside `IRulesProvider` (the §3.1 taxonomy).
2. Document the `IMovementCostProvider` contract and the `override ?? entityDefault` resolution model.
3. Add a house-rule example to the docs (the §3.5 composition).

---

## 7. Acceptance Criteria

| # | Criterion | Verifies |
|---|---|---|
| 1 | A new terrain type (`SwampTerrain`) works with every existing rules provider without modifying any provider | Open-Closed |
| 2 | A house-rule set can override any single rule (e.g., heat threshold) without reimplementing the entire rule set | Composition |
| 3 | `MechFactory` depends only on `IUnitConstructionRules` (1 method), not the full ~30-method interface | Interface Segregation |
| 4 | No `IRulesProvider? = null` optional parameters exist in the codebase | Information Hiding |
| 5 | No `DefaultValue` consts remain on modifier records | SRP |
| 6 | Terrain `Height`, `InterveningFactor` remain on the entity (intrinsic data) | Data/Rules boundary |
| 7 | `IMovementCostProvider` lives in Map; `TotalWarfareMovementCostProvider` lives in Core; neither references the other's concrete type | DIP |
| 8 | Adding a new domain interface (e.g., `IWeaponRules`) does not modify existing domain interfaces | Interface Segregation |
| 9 | All existing tests pass after each phase | Regression |
| 10 | A custom `IMovementCostProvider` can be injected into `BattleMap` and affects pathfinding | Testability |

---

## 8. Risks and Mitigations

| Risk | Mitigation |
|---|---|
| Large diff in Phase 3 (touching bots, MCP, MovementState) | Phased landing; each phase is independently testable and revertible |
| Performance regression from indirection | The facade delegates to the same in-memory tables; no I/O, no reflection. The `override ?? default` pattern is a dictionary lookup + null check — negligible in a turn-based game. |
| Breaking external consumers of `IRulesProvider` | The facade is source-compatible: existing `IRulesProvider` references still work. The domain split is additive. |
| Cache invalidation complexity (Phase 2) | Follow #1451's proven approach: invalidate the path cache when the provider reference changes. |

---

## 9. Non-Goals

- **Runtime rule switching** (hot-swapping providers mid-game). The architecture supports it, but no UI or game-flow support is in scope.
- **GameOptions system** (MegaMek's ~200+ options). MakaMek has no option-driven rules; this is future work.
- **Era/tech-level/faction rules.** Out of scope for the current game scope.
- **Mod support / scripting.** The composition model enables it, but no mod API is defined here.
- **Alpha Strike / BattleForce rules.** The facade supports multiple rule sets, but only Classic BattleTech is implemented.

---

## 10. Summary of Decisions

| Decision | Justified by |
|---|---|
| Entities are POCOs; services resolve rules | Information Hiding, Deep Modules |
| `override ?? entityDefault` for terrain cost | Open-Closed, Information Hiding |
| Provider segregated into domain interfaces | Interface Segregation, SRP |
| Facade composes domains | Composition, Deep Modules |
| Adapter (`TotalWarfareMovementCostProvider`) instead of inheritance | DIP, Composition Over Inheritance |
| Modifiers resolved in mechanics services, not unit APIs | SRP, Deep Modules, Information Hiding |
| House rules are compositions, not subclasses | Composition Over Inheritance, OCP |
| `GetStructureValues` behind `Construction` property | Interface Segregation (construction-only concern) |
| Close all four PRs; implement the refactor directly | The PRs' plumbing is incompatible with the target architecture |

---

**Document Version:** 1.0
**Author:** AI-assisted architecture analysis
**Reviewers:** TBD
