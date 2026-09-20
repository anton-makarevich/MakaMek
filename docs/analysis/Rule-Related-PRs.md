# Rules Provider — Consolidated Report

**Date:** 2026-09-20
**Status:** Decision aid for the four open "rules provider" PRs
**Related docs:** [`Rules-Provider-Analysis.md`](./Rules-Provider-Analysis.md) · [`Rules-Provider-Analysis-Updated.md`](./Rules-Provider-Analysis-Updated.md)
**Related issues:** #1105 (unify modifier values), #1028 (movement cost via rules provider)

This report consolidates the two prior analysis documents, describes the four open PRs (what each one actually does, where they overlap and conflict), and gives updated recommendations that fold in the maintainer's position on the open design questions.

---

## 1. Current situation

Today the codebase mixes two ways of expressing the same kind of information — a game-rule number:

- **Hardcoded on the entity.** Terrain classes in `src/MakaMek.Map/Models/Terrains/` expose intrinsic values as `abstract` properties: `Height`, `InterveningFactor`, `MovementCost`. Modifier records (`ProneAttackerModifier`, `SkiddingAttackerModifier`, …) carry `DefaultValue` consts. `Mech` computes its own heat→movement / heat→attack penalties inline, and hardcodes life-support pilot damage in `ApplyHeatEffects`.
- **Centralized in `IRulesProvider`.** Piloting/hit-location/cluster/structure/heat-threshold tables live behind `TotalWarfareRulesProvider`. The provider is a ~30-method interface, owned by `BaseGame` and injected into `ServerGame`/`ClientGame` and the mechanics calculators (e.g. `ToHitCalculator`).

Two facts from the current code that matter for the decisions below:

- **`GetStructureValues(tonnage)` is a construction-only concern.** It is called exactly once, in `MechFactory.CreateParts`, to size internal structure while building a `Mech`. After construction it is never read again. `MechFactory` already receives the provider — the unit itself does not.
- **On `main`, units do NOT hold a provider.** `Mech.GetAttackModifiers(PartLocation)` and `GetMovementPoints(MovementType)` take no provider; they read `*.DefaultValue` consts / compute penalties themselves. The provider lives on the game and reaches the domain only through the mechanics services that already take it. The four PRs are what would newly push the provider *into* the unit API.

So the inconsistency issue (#1105) and the terrain-cost issue (#1028) are real: the same class of value is sourced two different ways, and terrain cost cannot be changed by a rule set / house rule without editing terrain classes.

---

## 2. What the prior analyses recommended

**`Rules-Provider-Analysis.md` (v1.0 — "centralize everything").**
Recommended standardizing on the centralized provider *everywhere*, including injecting `IRulesProvider` into terrain classes so `Height`/`MovementCost`/etc. all resolve through the provider. Strong on maintainability/testability/rule-set support; weak on extensibility — it makes the provider the single source for values that are really per-entity content.

**`Rules-Provider-Analysis-Updated.md` (v2.0 — "hybrid").**
Flags the Open-Closed problem: a pure provider forces a `switch` edit for every new terrain/weapon. Recommends a **hybrid**: entity keeps a *default* value, the provider exposes an *optional override* keyed by the entity, and the value resolves as `providerOverride ?? entityDefault`. New entities keep working with every provider; a rule set overrides only what it changes. It also notes the residual coupling: introducing a new terrain still touches the `MakaMekTerrains` enum, so Core is never fully "closed."

The maintainer agrees with the hybrid direction, with two clarifications (see §6): the hybrid is the right *taxonomy*, and BattleTech genuinely has lookup tables for almost everything (weapons, terrain, textures), keyed by an entity id — so "entity default + table override keyed by entity" mirrors the source material.

---

## 3. Overview of the open PRs

All four are OPEN against `main`, none merged. PRs live at `https://github.com/anton-makarevich/MakaMek/pull/<n>`.

| PR | Title | Issue | Size (files, +/‑) | Scope |
|---|---|---|---|---|
| 1443 | Unify attack modifier rule values | #1105 | 17, +55/‑31 | Attack/to-hit modifiers only |
| 1450 | Unify rule-driven unit modifiers | #1105 | 23, +554/‑113 | Heat, skid, falling, life-support **+ all of 1443** |
| 1445 | Use rules provider for movement costs | #1028 | 13, +149/‑15 | Terrain MP cost via provider (keeps terrain defaults) |
| 1451 | Make terrain movement costs rule-provider driven | #1028 | 37, +222/‑157 | Terrain MP cost via provider (**deletes** terrain defaults) |

### 1443 — Unify attack modifier rule values
Removes the `DefaultValue` consts from `PartialCoverModifier`, `ProneAttackerModifier`, `SkiddingAttackerModifier`, `SkiddingTargetModifier`; adds `GetSkiddingAttackerModifier()`/`GetSkiddingTargetModifier()` to `IRulesProvider`/`TotalWarfareRulesProvider`; changes `GetAttackModifiers` on `IUnit`/`Unit`/`Mech` to take the provider, and threads it through `AttackScenario`, `ToHitCalculator`, and the bot `TacticalEvaluator`. Uses the **optional `IRulesProvider? = null`** pattern, so `AttackScenario` branches on null. Narrow and focused; the cleanest of the four in isolation.

### 1450 — Unify rule-driven unit modifiers
The largest PR and a **superset of 1443** for attack modifiers. Adds `GetHeatMovementPenalty`, `GetHeatAttackPenalty`, `GetLifeSupportPilotDamage`, `GetFallingLevelsModifier` **and the same skidding getters 1443 adds**. Threads the provider into `GetMovementPoints`, `GetDamageReducedMovement`, `CanStandup`, `CanChangeFacingWhileProne`, and downstream through `IUnit`/`Unit`/`Mech` (Mech alone +79/‑61), `UnitExtensions`, `MovementState`, bot `MovementEngine`, MCP `MovementTools`, `MovementPhase`, `BaseGame`, `PilotingSkillCalculator`, `FallBroadcastAction`. Retains **parameterless "legacy wrappers"** (a third source of truth) for compatibility. ⚠️ It also commits `BUG-INVENTORY.local.md` (195 lines) — a local artifact that must not land.

### 1445 — Use rules provider for movement costs
Introduces `IMovementCostProvider` and `DefaultMovementCostProvider` in the **Map** layer, adds `Hex.GetEnterMovementCost(..., IMovementCostProvider? = null)` and `BattleMap.MovementCostProvider`, and wires the active provider through `ServerGame`/`BaseGame` and `SkidInterruptHandler`. **Keeps** `Terrain.MovementCost` as the standalone-map default. Result: the terrain-cost table now exists **twice** — in `DefaultMovementCostProvider` (Map) and `TotalWarfareRulesProvider` (Core).

### 1451 — Make terrain movement costs rule-provider driven
Same seam as 1445, but the "full" version: it **deletes** the `MovementCost` property from `Terrain` and every subclass (Clear/LightWoods/HeavyWoods/Rough/Water/Road/Pavement/Bridge/Rubble) and their tests, and adds **path-cache invalidation** when movement-cost rules change (`BattleMap` +23/‑3). Larger blast radius (37 files) because it removes the terrain-default tests.

### Overlaps and conflicts
- **1443 ⊂ 1450 (duplicate).** 1450 re-implements everything 1443 does (same const removals, same skidding getters, same `GetAttackModifiers` change). Landing both produces duplicate/conflicting definitions. **Choose one lineage**, not both.
- **1445 vs 1451 (mutually exclusive).** Both touch the identical seam (`IMovementCostProvider`, `DefaultMovementCostProvider`, `Hex.GetEnterMovementCost`, `ServerGame`/`BaseGame` wiring, `SkidInterruptHandler`). 1451 = 1445 **+ deletion of terrain defaults + cache invalidation**. They will conflict directly. **Pick one.**
- **Cross-pair textual conflicts.** 1450 and 1445/1451 both edit `BaseGame`, `ToHitCalculator`, `AttackScenario`, `IRulesProvider`, and `TotalWarfareRulesProvider` — expect merge conflicts regardless of order.
- **Version-bump collision.** All four bump `Directory.Build.props` (1443/1445 → `0.63.40`, others overlapping). Per repo rule the second to merge must re-bump.
- **Stray file.** 1450's `BUG-INVENTORY.local.md` must be removed before merge.

## 4. The concerns are justified

Four concrete problems, independent of the data-vs-rules question:

1. **Optional/nullable provider parameters.** `GetAttackModifiers(PartLocation location, IRulesProvider? rulesProvider = null)` and `Hex.GetEnterMovementCost(..., IMovementCostProvider? = null)` create two sources of truth. `AttackScenario` literally branches on it:

<augment_code_snippet path="src/MakaMek.Core/Data/Game/Mechanics/AttackScenario.cs" mode="EXCERPT">
```csharp
AttackerModifiers = rulesProvider is null
    ? attacker.GetAttackModifiers(weaponLocation)
    : attacker.GetAttackModifiers(weaponLocation, rulesProvider),
```
</augment_code_snippet>

A house rule set that changes the prone modifier silently does nothing on any call path that forgot to pass the provider. Also `Mech.GetAttackModifiers` calls `rulesProvider.GetProneFiringModifier()` on a parameter declared nullable — a latent NRE.

2. **Duplicated rule tables.** The identical terrain-cost switch now exists in both `DefaultMovementCostProvider` (Map) and `TotalWarfareRulesProvider` (Core). Two copies of the same rule is exactly what the PRs set out to eliminate.

3. **`IRulesProvider : IMovementCostProvider` is the wrong direction.** It makes the whole Core rules surface a subtype of a Map concern, and forces every alternative rules provider to implement map pathfinding costs. Composition (`IRulesProvider.Movement` returning an `IMovementCostProvider`) or an adapter would keep the seam clean.

4. **Provider as a method parameter, not a dependency.** `GetMovementPoints(type, rulesProvider)`, `CanStandup(rulesProvider)`, `GetDamageReducedMovement(rulesProvider)` push a service through the domain API of every unit. The ripple is visible: `IUnit`, `Unit`, `Mech`, `MovementState`, bots, MCP tools, `UnitExtensions`. Units already live inside a game that owns the provider; the dependency belongs at construction/assignment time, not on every call site.

## 5. Where the line should be: data vs rules vs state

The useful distinction is not "is it a number in BattleTech" but **what varies together, and along which axis**.

**Game data — stays in entities / data files.** Identity and intrinsic description of a *thing*. Varies per-instance, scales with content, and is authored (MTF import, map files).
- Terrain identity, height, water depth, bridge construction factor, intervening factor
- Weapon name, damage, heat, ranges, tonnage/slots, BV, ammo type
- Unit tonnage, base movement, armor/structure layout, crit slot layout

**Rules — go through `IRulesProvider`.** Procedures and tables of the *rule set*. Bounded in count, varies when you switch Total Warfare → house rules, and are the things a rule set actually rewrites.
- Table lookups: hit location, cluster hits, structure-by-tonnage, heat scale effects, PSR modifiers
- Situational to-hit/PSR modifiers: prone, skidding, partial cover, range, movement, terrain to-hit
- Thresholds and target numbers: shutdown, ammo explosion, heavy-damage, external-heat cap
- Cost/consumption formulas: MP cost of entering terrain, heat per movement type

**State — neither.** Current heat, MP spent, damage, prone/skidding flags. Lives on the unit.

Applying that test to the PRs:

| Value | PR treats as | Should be | Why |
|---|---|---|---|
| Heat→movement / heat→attack penalty tables | rule | **rule** ✅ | Classic heat scale; a house rule replaces the whole table |
| Skidding attacker/target modifiers, prone firing, partial cover | rule | **rule** ✅ | Situational to-hit modifiers |
| Falling-levels modifier | rule | **rule** ✅ | Formula, not data |
| Life-support pilot damage at heat 15/26 | rule | **rule** ✅ | Currently hardcoded in `Mech.ApplyHeatEffects` — correct to extract |
| Terrain *entry MP cost* | rule | **rule**, but keyed by terrain | It is a rule-set table, not a property of the woods |
| Terrain height / intervening factor / water depth | data (untouched) | **data** ✅ | Correctly left alone |
| Weapon damage / heat / ranges | data (untouched) | **data** ✅ | Content; scales with catalogue size |

So the *classification* in these PRs is essentially right. The terrain movement cost genuinely is a rule (it is per-rule-set, and the count of terrain types is bounded). The problem is the **plumbing**, not the taxonomy.

The one thing worth calling out: routing terrain cost through `GetMovementCost(MakaMekTerrains, int)` re-introduces the Open-Closed problem that `docs/analysis/Rules-Provider-Analysis-Updated.md` already flagged — a new terrain type now forces a modification of every rules provider, and both PRs throw `ArgumentOutOfRangeException` on unknown terrain. That document's recommended hybrid (entity default + optional provider override) is the safer shape:

```csharp
int GetTerrainMovementCost(Terrain terrain) => /* provider override */ ?? terrain.DefaultMovementCost;
```

A new `SwampTerrain` then works with every rules provider, and a house rule can still override it.

## 6. Maintainer's position (agreed / open)

Two points from the prior analyses are **accepted**:

1. **Hybrid, table-keyed-by-entity.** The entity carries a value; a rule set's lookup table can override it by the entity's key; if the table has no entry for that key, the entity default stands. This mirrors BattleTech itself, which is table-driven for nearly everything (weapons, terrain, even textures), all keyed by an id. This is exactly the `providerOverride ?? entityDefault` shape from §5.
2. **Per-domain rules, not a monolith.** `IRulesProvider` should not be one god interface. In particular, `GetStructureValues(tonnage)` is **only** used inside `MechFactory` during construction and never again — so it does not belong on an interface threaded through the whole runtime. Segregate by domain (`IHeatRules`, `IToHitRules`, `IPilotingRules`, `IMovementRules`, `IDamageRules`, construction-time `IUnitConstructionRules`) composed behind a facade, so a house-rule set is `new RulesProvider(TotalWarfare.Heat, MyHouse.ToHit, …)` and construction-only tables stay out of the hot path.

One point is **still open**:

- **Should an entity depend on the provider at all?** The maintainer is not convinced a `Unit`/`Terrain`/weapon should take `IRulesProvider` in its constructor (or hold a reference). Injecting the provider into the entity is a lighter version of the same coupling the method-parameter approach creates — it still makes a plain data object depend on a rule-set service.

### Resolving the open question

The hybrid model does **not** require the entity to know the provider. Keep entities as plain data (their intrinsic default value), and let **the service that already owns the provider** perform the override lookup, keyed by the entity:

```csharp
// entity stays a POCO — just a default
public abstract class Terrain { public abstract int MovementCost { get; } /* default */ }

// the provider does the keyed override, falling back to the entity's default
int GetTerrainMovementCost(Terrain terrain)
    => _overrides.TryGetValue(terrain.Id, out var v) ? v : terrain.MovementCost;

// callers that already hold the provider (pathfinding, ToHitCalculator, MechFactory) call it;
// the entity never references IRulesProvider.
```

This satisfies both accepted points and the open concern simultaneously:
- entities keep intrinsic defaults (Open-Closed: a new `SwampTerrain` just works);
- the rule set can still override by key;
- **no entity depends on the provider** — the dependency lives only where it already lives (the game and its mechanics/factories).

For runtime per-unit modifiers (heat/prone/skidding/falling) the same principle applies: these are already assembled inside mechanics services that own the provider (`ToHitCalculator`, piloting/fall calculators, `MechFactory`). Move the assembly there instead of adding `provider` parameters to `Mech.GetAttackModifiers`/`GetMovementPoints`. If a unit truly needs a value on its own API, resolve it from the unit's **owning game** (which holds the provider), not from a per-call parameter and not from a constructor-injected provider on the entity.

## 7. Updated recommendations / disposition

- **Attack + unit modifiers (1443 / 1450): keep one lineage.** They duplicate each other. Recommended: take **1450's superset** (heat + skid + falling + life-support) but **close 1443** rather than merging both. Before merge, rework 1450 to:
  - drop the **optional `IRulesProvider? = null`** parameters and the **parameterless legacy wrappers** (both create the "silently does nothing" second/third source of truth from §4.1);
  - resolve the provider inside the mechanics services / owning game, not on `Mech`/`Unit`/`IUnit` method signatures — this also shrinks the `MovementState`/bots/MCP ripple;
  - remove the committed `BUG-INVENTORY.local.md`.
  If reworking 1450 wholesale is too large, land the small, clean **1443** first (still dropping the optional param) and rebase a reduced 1450 on top so it stops re-declaring the skidding getters.
- **Terrain movement cost (1445 / 1451): pick one, prefer the hybrid.** Both duplicate the cost table across Map and Core (§4.2) and invert the dependency via `IRulesProvider : IMovementCostProvider` (§4.3). Recommended shape:
  - keep `IMovementCostProvider` in Map as the seam, but have Core **supply an adapter** rather than making `IRulesProvider` inherit it (composition, not inheritance);
  - make the single source of truth the **entity default + keyed override** (§6) so `DefaultMovementCostProvider` delegates to `terrain.MovementCost` instead of re-listing every terrain, and unknown terrains fall through to the default instead of throwing `ArgumentOutOfRangeException`.
  - Between the two: **1451's** direction (removing hardcoded terrain properties) only makes sense if the value moves to a keyed table with a default fallback; as written it deletes the default that the hybrid relies on. **1445** keeps the default but duplicates the table. Cleanest is to take **1445's smaller surface** and refactor `DefaultMovementCostProvider`/`TotalWarfareRulesProvider` to the keyed-override-over-entity-default model, so no table is duplicated and no terrain default is lost.
- **Interface segregation is prerequisite work.** Before (or alongside) the above, split `IRulesProvider` by domain and move `GetStructureValues` behind a construction-only rules type used by `MechFactory`. This is what makes mixable house rules practical and stops construction-only tables from being threaded through the runtime.
- **Permanent boundary.** Keep terrain geometry/LOS data (height, intervening factor, water depth) and all weapon/unit stats as entity data; document that boundary next to `IRulesProvider`.

### Suggested next steps (ordered)
1. Land the interface segregation + move `GetStructureValues` to a construction-time rules type (no behavior change, unblocks the rest).
2. Decide the movement-cost lineage (recommend 1445 refactored to keyed-override-over-default; close 1451). Land it.
3. Decide the modifier lineage (recommend reworked 1450; close 1443, or land 1443 then reduce 1450). Land it.
4. Add a short "data vs rules vs state" note beside `IRulesProvider` to lock the boundary.
