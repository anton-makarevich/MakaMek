## Related docs
docs/analysis/Rules-Provider-Analysis.md
docs/analysis/Rules-Provider-Analysis-Updated.md

## Status of the four PRs

PRs are in https://github.com/anton-makarevich/MakaMek/pull/

| PR | Title | State | Essence |
|---|---|---|---|
| 1443 | Unify attack modifier rule values | OPEN | Removes `DefaultValue` consts from `*Modifier` records; adds `GetSkiddingAttackerModifier()` / `GetSkiddingTargetModifier()`; `GetAttackModifiers(location, IRulesProvider? = null)` |
| 1450 | Unify rule-driven unit modifiers | OPEN | Adds `GetHeatMovementPenalty`, `GetHeatAttackPenalty`, `GetLifeSupportPilotDamage`, `GetFallingLevelsModifier` + skidding; threads `IRulesProvider` into `GetMovementPoints`, `GetDamageReducedMovement`, `CanStandup`, `CanChangeFacingWhileProne` |
| 1445 | Use rules provider for movement costs | OPEN | Movement cost via provider |
| 1451 | Make terrain movement costs rule-provider driven | OPEN | Deletes `Terrain.MovementCost`; adds `IMovementCostProvider` in Map, `IRulesProvider : IMovementCostProvider`, `BattleMap.MovementCostProvider`, `Hex.GetEnterMovementCost(..., provider?)` |

1443/1450 and 1445/1451 are overlapping pairs — 1450 re-adds the same skidding getters 1443 adds, and both movement-cost PRs touch the same seam. None are merged; `main` still has `GetAttackModifiers(PartLocation)` and `GetMovementPoints(MovementType)`.

## The concerns are justified

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

## Where the line should be: data vs rules vs state

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

## For swappable + mixable rule sets

The current `IRulesProvider` is a 30+-method god interface. Mixing house rules means writing a full decorator that forwards everything. Two changes make mixing practical:

1. **Segregate by domain** — `IHeatRules`, `IToHitRules`, `IPilotingRules`, `IMovementRules`, `IDamageRules`, composed by `IRulesProvider`. Then a house rule set is `new RulesProvider(TotalWarfare.Heat, MyHouse.ToHit, TotalWarfare.Piloting, ...)`.
2. **Inject once, never pass as a parameter.** Units/mechanics get the provider at construction (or via the game they belong to). Every optional `IRulesProvider?` parameter added by 1443/1450 should become a non-optional constructor dependency or be removed in favour of the unit's owning-game reference.

## Suggested disposition

- **1443 + 1450**: merge the concept, but reject the `IRulesProvider? = null` parameter pattern — collapse them into one PR (they duplicate the skidding getters) and resolve the provider from the unit's game context instead of the call signature.
- **1445 + 1451**: pick one. Keep `IMovementCostProvider` in Map as the seam, but have Core *supply* an adapter rather than making `IRulesProvider` inherit it, and delete the duplicated cost table by making `DefaultMovementCostProvider` delegate to terrain defaults (hybrid model) rather than re-listing every terrain.
- Keep terrain geometry/LOS data and all weapon/unit stats out of the provider permanently — document that boundary next to `IRulesProvider`.

Note the version-bump rule: these four PRs all touch `Directory.Build.props`, so whichever lands second will conflict and needs a re-bump.