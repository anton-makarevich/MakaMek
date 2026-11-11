# Physical Attack Level and Water Rules

How physical-attack eligibility resolves the level and water state of an attacker and a target, why two services currently own overlapping versions of those facts, and which parts of the rule must stay local to each attack.

This document is the decision record for issue [#1685](https://github.com/anton-makarevich/MakaMek/issues/1685). It is a follow-up to the Charge/DFA declaration work in PR [#1684](https://github.com/anton-makarevich/MakaMek/pull/1684) and its rules-review comment. It records an investigation and a decision only: no production behaviour changes are made here. The code changes are defined as follow-up issues at the end.

## Status and scope

- **In scope:** the duplicated endpoint facts (effective standing level, submersion, in-water) and the water/level rules used by `PhysicalAttackEligibility` (Punch, Kick, Push) and `MovementAttackDeclarationValidator` (Charge, DFA).
- **Out of scope:** Charge/DFA hit resolution, underwater damage halving (tracked by [#1503](https://github.com/anton-makarevich/MakaMek/issues/1503)), hull-breach resolution, and any change to the Different-Levels table itself.
- The decision **qualifies** the [#1612](https://github.com/anton-makarevich/MakaMek/issues/1612) decision. It does not supersede it.

## Current implementation

Physical-attack logic lives in `src/MakaMek.Core/Models/Game/Mechanics/PhysicalAttack/`.

- `PhysicalAttackEligibility` is the replaceable, game-owned policy for the current board state. It resolves a private `UnitEndpoint(EffectiveStandingLevel, IsSubmerged, IsInWater)` from the map and applies:
  - Punch/Kick: a signed, height-adjusted Different-Levels table.
  - Push: exact equality of effective standing levels.
  - A target-side water check for Punch, Kick and Push.
- `MovementAttackDeclarationValidator` is a separate class with its own private `UnitEndpoint(StandingLevel, IsSubmerged)`. `MovementPhase` constructs it by default; `BattleTechPhaseManager` does not inject a replacement. It applies:
  - Charge: the `From` position of the last translating segment (the **approach position**), `abs(target − approach) <= 1`, and the same submersion flag on both endpoints.
  - DFA: the attacker's start position, no level limit, rejection of any submerged endpoint, and a jump-clearance check.
- Both services call `HexExtensions.GetLosEndpointInfo(surface, height)` from `src/MakaMek.Map/Models/HexExtensions.cs`. That helper returns the effective standing level and the submersion flag but not an in-water flag. Its airborne contract is written for line of sight, not for physical attacks.
- Jump-jet availability per location is a separate rule in `JumpJets` / `UnitPart.IsSubmerged`, and it reads the stored `Unit.Hex`; the endpoint helpers read the map.

Related documents:

- `docs/rules/physical-attacks.md` §6 holds the water rules.
- `docs/project/physical-attacks-prd.md` holds the eligibility-policy decision and the Charge/DFA declaration requirements.
- Underwater damage halving is tracked by [#1503](https://github.com/anton-makarevich/MakaMek/issues/1503).

## Comparison of the five attacks

| Attack | Declared | Endpoint source | Level comparison | Water predicate |
| :--- | :--- | :--- | :--- | :--- |
| **Punch** | Physical Attack Phase | Current positions | Signed, height-adjusted (`target − attacker − (targetHeight − attackerHeight)`); Different-Levels table | If the target is submerged, the attacker must be submerged |
| **Kick** | Physical Attack Phase | Current positions | Signed, height-adjusted; Different-Levels table | If the target is submerged, the attacker must be in water (a Depth 1 kick is entirely underwater) |
| **Push** | Physical Attack Phase | Current positions | Exact equality of effective standing levels | If the target is submerged, the attacker must be submerged |
| **Charge** | Movement Phase | Approach position: `From` of the last translating segment for the attacker; target's current position | `abs(target − approach) <= 1` on effective standing levels | Both endpoints must be on the same side of the waterline (both submerged or both not submerged) |
| **DFA** | Movement Phase | Attacker's start position for the water/jump-jet guard; target's position for the guard and the clearance check | None: DFA bypasses the Different-Levels table | Target must not be submerged; attacker must not be submerged (jump-jet capability) |

## Shared and divergent facts

The two services genuinely share:

- Map lookup through `GetHex`, not through the stale `Unit.Hex`.
- The standing level and submersion derived by `GetLosEndpointInfo`.
- The fact that water depth counts as negative levels, and that a bridge is not water.
- A signed raw level difference (`target − attacker`) that each attack then interprets locally.
- An in-water flag that only `PhysicalAttackEligibility` currently derives.

They do **not** share (and must not share):

- The Punch/Kick height adjustment (`−(targetHeight − attackerHeight)`).
- The Punch/Kick Different-Levels table and its prone-target variants.
- The Push exact-equality rule.
- The Charge absolute limit and the choice of the approach position.
- The DFA jump-clearance rule.

## Evidence

### Counterexample to the ticket premise

The issue suggested that a fully submerged attacker is at least two levels below its own water surface and that any target within the ±1 level limit is therefore at least partially submerged, so "submerged attacker → submerged target" might be implicit in the level rule.

The test `Water_SubmergedAttackerAgainstLandTargetOneLevelLower_PunchAllowed` disproves that for Punch. The submerged attacker stands at effective level **−2** (Depth 2 water on a level 0 hex). The land target stands at effective level **−1**. The signed difference is **+1**, a legal punch on the Different-Levels table, and the target is **not** submerged. The level rule therefore does not imply the submerged→submerged case.

This counterexample applies to Punch/Kick/Push. It does not apply to Charge, because a Charge is a movement attack: the charging unit physically enters the target hex, and the rules require both endpoints to be on the same side of the waterline.

### Evidence limits

- The PR [#1684](https://github.com/anton-makarevich/MakaMek/pull/1684) rules-review comment **is accessible**. It claimed that accepting a DFA whose target stands in Depth 1 water was a Total Warfare violation, on the ground that a 'Mech cannot jump directly into a Depth 1-or-deeper water hex. The maintainer clarification of 2026-11-09 rejects that reading: a standing 'Mech in Depth 1 water is only waist-deep and is **not** submerged, so it remains a legal DFA target, and the jump lands in the water hex with a landing PSR. The review comment is recorded as superseded, not as a defect.
- The Charge test `HandleCommand_WhenTheChargeTargetIsSubmerged_ShouldRejectTheCharge` is **confounded**. Its target hex has raw level 1 with Depth 2 water, so the target's effective standing level is −1 against an approach level of 1 — a two-level difference. The level check alone rejects it, so the test does not isolate the water rule.
- The endpoint helper uses `Position.AirborneLevel` when it is non-null: the airborne base level replaces the surface standing level for physical reach, and the endpoint is never submerged or in water. A target two levels above is therefore out of physical reach as well as covered by the p. 149 DFA immunity.
- The DFA clearance formula (`Jump MP >= targetStandingLevel + target.Height`) does not subtract the start standing level. This is recorded as an open question, not as a defect.
- `Unit.IsSubmerged` / `UnitPart.IsSubmerged` and the map-based endpoint can disagree after a map hex is replaced, because they read the stored `Unit.Hex`; they can also disagree for a unit on a bridge over water, because `UnitPart.IsSubmerged` ignores the occupied surface. This matters to the jump-jet guard and is recorded as an open question.

## Decisions

### D1 — One Core endpoint record and one map extension

Add one Core record for physical-attack endpoint facts in `src/MakaMek.Core/Models/Game/Mechanics/PhysicalAttack/`:

```csharp
public readonly record struct PhysicalAttackEndpoint(
    int EffectiveStandingLevel,
    bool IsSubmerged,
    bool IsInWater);
```

Add one extension for the map in the same folder. It takes a `HexPosition` and a unit height, returns `null` when the hex is missing, wraps `GetHex` and `GetLosEndpointInfo`, and adds the in-water flag. When `Position.AirborneLevel` is set (a pending DFA approach position), that absolute base level replaces the surface standing level and the endpoint is never submerged or in water:

```text
map.GetPhysicalAttackEndpoint(position, unitHeight) -> PhysicalAttackEndpoint?
```

The in-water flag is `position.Surface == HexSurface.Ground && hex.GetWaterDepth() > 0`. A bridge is never in water. An airborne position is never in water.

Both services replace their private `UnitEndpoint`/`GetEndpoint` with this record and extension. This follows the `<Type>Extensions` convention in `AGENTS.md`.

### D2 — Why the extension is in Core and not in Map

Physical attacks use an explicit occupied surface and, for a pending DFA approach position, its airborne base level. Line of sight uses a different airborne contract and different reference levels (LOS adds the unit height on top of the base; physical reach compares the base levels). Putting the physical-attack endpoint in `MakaMek.Map` would either expose LOS behaviour to physical attacks or force the Map layer to know about physical-attack semantics. The record and extension therefore live in the Core physical-attack module, above the Map primitives they compose.

### D3 — One signed level primitive; comparisons stay local

The shared primitive is the signed raw difference:

```text
target.StandingLevel − attacker.StandingLevel
```

Each attack keeps its own comparison:

- Punch/Kick: add the height adjustment and consult the Different-Levels table. This stays in `PhysicalAttackEligibility`, as the [#1612](https://github.com/anton-makarevich/MakaMek/issues/1612) decision requires.
- Push: exact equality, local to `PhysicalAttackEligibility`.
- Charge: `abs(target − approach) <= 1`, local to the movement validator.
- DFA: no level comparison.

### D4 — The canonical water rule is attack-specific

The rules are not one rule applied uniformly. The common target-side wording is:

> If the target is fully submerged, the attack must take place entirely underwater.

For Punch, Kick and Push this is the Total Warfare p. 151 rule. There is no attacker-side restriction toward a surface target: a fully submerged attacker may punch or push a surface target subject to the normal requirements, and a Depth 1 attacker may make any attack against a surface target. The condition that makes each attack entirely underwater is:

| Attack | Attack takes place entirely underwater when |
| :--- | :--- |
| **Punch** | The attacker is fully submerged |
| **Kick** | The attacker is in water (a standing 'Mech in Depth 1 water has submerged legs) |
| **Push** | The attacker is fully submerged |
| **Charge** | Both the attacker (at the approach position) and the target are fully submerged |
| **DFA** | Never |

Charge adds a symmetric target-isolation rule. Per the maintainer clarification of 2026-11-09, and consistent with the Total Warfare p. 151 Depth 1 wording and the movement nature of the attack:

- A fully submerged 'Mech can charge another fully submerged unit.
- A fully submerged 'Mech cannot charge a surface or Depth 1 unit, and a surface/Depth 1 unit cannot charge a submerged unit.
- A standing 'Mech in Depth 1 water can charge another standing unit, because neither is fully submerged.
- A charge cannot be declared against a submerged target by a non-submerged attacker, including a submarine resting at Depth 1 and a prone 'Mech in Depth 1 water.

DFA is an explicit exception. Because DFA bypasses the Different-Levels table, its rejection of a submerged target cannot be inferred from a level comparison and must stay an explicit check. The shared water predicate returns "entirely underwater = false" for DFA.

The predicate is a small shared function keyed by `PhysicalAttackType`, not a policy interface. A useful form is:

```text
if (attacker.IsSubmerged || target.IsSubmerged)
    the attack must be entirely underwater
```

where "entirely underwater" is evaluated per the table above. For Charge this reduces to `attacker.IsSubmerged == target.IsSubmerged`. For Punch/Push it reduces to the attacker being submerged; for Kick to the attacker being in water. This keeps the two services from diverging again without moving the attack-specific comparisons.

### D5 — DFA: explicit target guard and a jump-jet guard

- **Submerged target:** DFA rejects it explicitly. The rejection is not derived from the level rule and must remain an explicit check, because DFA has no level comparison.
- **Submerged attacker:** the rejection is a **jump-jet capability guard** ("jump jets cannot be fired while submerged"), not a physical-attack water rule. A Depth 1 'Mech can still jump with its torso-mounted jets; a fully submerged 'Mech cannot jump at all. Jet availability per location is already expressed by `JumpJets` / `UnitPart.IsSubmerged`, and that rule reads the stored `Unit.Hex`.
- **Target standing in Depth 1 water:** this is a legal DFA target. A standing 'Mech is waist-deep, so its torso and head remain above the surface and it is not submerged. The attack is not "entirely underwater" and does not need to be: the target-side rule only protects a **fully submerged** unit. The attacker needs enough Jumping MP to clear the target hex's standing level plus the target's height (for a Depth 1 hex at level 0 the waterbed is −1 and a standing 'Mech reaches +1). If the jump lands in the water hex, the attacker makes a landing PSR; on success it stands on the waterbed, on failure it falls onto the waterbed.
- The claim in the PR [#1684](https://github.com/anton-makarevich/MakaMek/pull/1684) rules-review comment that this case is illegal is **not adopted**. The "cannot jump directly into water" rule describes how a normal jump ends over a water hex and then resolves its landing; it does not make a standing Depth 1 target submerged or remove it from the DFA target set.

### D6 — Relationship to #1612

The fact-only endpoint record and the small water predicate **qualify** the [#1612](https://github.com/anton-makarevich/MakaMek/issues/1612) decision:

- The Different-Levels table stays local to `PhysicalAttackEligibility`.
- No `IPhysicalAttackEligibilityRules` interface is introduced.
- `IPhysicalAttackEligibility` remains the replaceable policy for Punch/Kick/Push.

They do not supersede #1612. A replacement of `IPhysicalAttackEligibility` does **not** change Charge or DFA, because those declarations are validated by a separate service. A separate follow-up must make the movement validator replaceable if house rules need to vary Charge/DFA.

### D7 — Airborne positions use their base level for physical reach

A pending DFA leaves the attacker airborne above its approach hex, with its feet at `max(target hex reference level, approach hex reference level) + 2` (*Total Warfare* p. 149-150 errata). That absolute base level is the unit's elevation for the Different-Levels table, so the endpoint extension uses `Position.AirborneLevel` when it is non-null instead of the hex surface standing level, and marks the endpoint as neither submerged nor in water.

- **No height added:** the base level replaces the surface standing level exactly as `GetLosEndpointInfo` does. LOS then adds the unit height on top of the base; physical reach compares the base levels of the two units.
- **Both mechanisms apply:** the airborne level can put the DFA performer out of reach on its own (for example `LevelDifferenceOutOfRange` for Punch/Kick and `ElevationMismatch` for Push), but the p. 149 immunity stays the authoritative rule. It blocks attacks even when terrain puts the airborne level within reach, and it is the reason the UI reports for a DFA performer.
- **Charge/DFA declarations:** the movement validator runs before the move, so its endpoints are grounded and its behaviour is unchanged.

## Effects on current behaviour

| Case | Current | After the follow-up code changes |
| :--- | :--- | :--- |
| Punch/Kick/Push against a submerged target from land | Rejected | Unchanged |
| Punch against a submerged target from Depth 1 | Rejected | Unchanged |
| Kick against a submerged target from Depth 1 | Allowed | Unchanged |
| Punch against a surface target from Depth 2+ | Allowed | Unchanged |
| Charge, both endpoints not submerged (including Depth 1 standing) | Allowed | Unchanged |
| Charge, both endpoints submerged | Allowed | Unchanged |
| Charge, submerged approach vs surface target | Rejected | **Unchanged** (the plan's proposed relaxation is rejected) |
| Charge, surface/Depth 1 approach vs submerged target | Rejected | Unchanged |
| DFA against a submerged target | Rejected | Unchanged (explicit guard) |
| DFA by a submerged attacker | Rejected | Unchanged (jump-jet guard) |
| DFA against a target standing in Depth 1 water | Allowed | Unchanged (the landing in water requires a landing PSR) |

The Charge rows are the important correction to the original plan. The plan proposed a one-sided rule for all attacks and would have changed a submerged charge against a surface target from rejected to allowed. The maintainer clarification confirms that this case must stay rejected: a submerged unit can only charge another underwater unit. The current equality check is therefore correct in outcome; the follow-up only routes it through the shared predicate.

## Charge interpretation

The Charge approach position is the `From` of the last translating segment, which the validator already treats as the charge origin. The alternative — requiring every segment of the path to be fully submerged — is rejected because the rules separate movement before the attack from the attack itself, and movement hazards in water already go through the movement interrupt handlers.

This interpretation was confirmed by the maintainer on 2026-11-09 and no longer needs a separate confirmation before the follow-up merges. The original plan marked it as pending because the PR [#1684](https://github.com/anton-makarevich/MakaMek/pull/1684) rules-review comment was recorded as inaccessible; that comment is accessible and is incorporated above.

The symmetric *Tactical Operations* rule for all physical attacks is **not adopted**. The codebase adopts the Total Warfare p. 151 one-sided target rule for Punch/Kick/Push and the charge-specific isolation rule above. A house rule can implement the broader symmetric rule through a replacement `IPhysicalAttackEligibility` (Punch/Kick/Push) and, later, a replacement movement validator (Charge/DFA).

## Resolution rules (context, not part of this decision)

These rules are recorded for completeness but are **not** implemented or changed here:

- Physical-attack damage applied to an underwater location is **halved, rounding down** ([#1503](https://github.com/anton-makarevich/MakaMek/issues/1503)).
- Non-naval units without Underwater Maneuvering Units pay **4 MP per hex** to move along a waterbed. Charge damage uses the hexes entered after the last direction reversal, so the higher underwater movement cost generally lowers the achievable charge velocity and damage.
- A successful charge hit underwater requires a **hull-breach check** on the receiving location, and on the attacker if it takes return collision damage. The `HullBreachCalculator` already exists and is applied generically to physical-attack damage; the charge-specific return collision belongs to the unimplemented Charge resolution.

## Official sources

- *Total Warfare* p. 109 — water depth counts as negative levels; the "underwater units cannot fire on or take fire from surface units" rule is a weapon-fire rule and is not the physical-attack rule.
- *Total Warfare* p. 151 (Water) — Depth 1: a 'Mech may make any physical attack but cannot attack an underwater unit unless the attack begins underwater too (the kick example); a charge/DFA against the adjacent submerged submarine is illegal. Depth 2+: the 'Mech may charge, club, kick, punch, push or make a physical-weapon attack; an attack against a surface vessel is underwater only if it originated underwater.
- *Total Warfare* pp. 149–151 — Charge and DFA declaration, Different Levels, DFA clearance.
- *Total Warfare* pp. 55 and 149–150 — jump movement and DFA landing. A normal jump may end over a water hex and then resolve a landing Piloting Skill Roll. A standing Depth 1 target is not submerged and remains a legal DFA target; if the DFA lands in the water hex, the attacker also makes a landing PSR.
- *A Game of Armored Combat* p. 25 (Physical Attacks and Water) — same target-side wording and the underwater-damage halving.
- *A Game of Armored Combat* p. 12 (Jumping Out of Water) — torso jets can be used from Depth 1; a fully submerged 'Mech cannot jump.
- Existing repository references: `docs/rules/physical-attacks.md` §6, the #1612 decision, and #1503.

## Rejected alternatives

1. **Apply one one-sided water rule to all five attacks.** Rejected. It would relax Charge from a submerged approach against a surface target, contradicting the target-isolation rule for submerged units and the maintainer clarification.
2. **Keep the private endpoint records and only share the water predicate.** Rejected. The two endpoint records are the same facts with different names, and duplicating them is what allowed the services to diverge.
3. **Put the endpoint extension in `MakaMek.Map`.** Rejected. Map's LOS airborne contract differs from the explicit occupied-surface contract physical attacks need.
4. **Extend `IPhysicalAttackEligibility` with a path-agnostic evaluation reused by the movement validator.** Rejected. Charge/DFA need the path, the movement budget and the server-computed `HexesMoved`; widening the replaceable interface would partly supersede #1612 and still not make the movement validator replaceable.
5. **Introduce `IPhysicalAttackEligibilityRules`.** Rejected by #1612; the existing registration is already replaceable.

## Known deviations and open questions

| Item | Status |
| :--- | :--- |
| DFA clearance formula does not subtract the start standing level | **Open question.** Compare with the jump ceiling in `BattleMap`; track in Issue C. |
| `Unit.IsSubmerged` / `UnitPart.IsSubmerged` (stored `Unit.Hex`, surface-unaware) and the map endpoint can disagree after a hex replacement **or for a unit on a bridge over water** | **Open question.** Affects the jump-jet guard only: a bridge over Depth 2 water can make torso `JumpJets` report `IsSubmerged` even though the map endpoint is not submerged. Track in Issue C. |
| `AirborneLevel` handling for attack endpoints | **Implemented (D7).** The endpoint uses `Position.AirborneLevel` when present, so a DFA performer is compared at its airborne base level and is never submerged. |
| Charge/DFA resolution, underwater half damage, hull breach on charge | **Not implemented.** #1503 and the Charge/DFA resolution milestones. |

## Follow-up issues

These are recorded here for the maintainer to file as separate, reviewable GitHub issues. None is implemented in this change.

### Issue A — Extract the shared endpoint primitive (behaviour-preserving) — [#1697](https://github.com/anton-makarevich/MakaMek/issues/1697)

- Add `PhysicalAttackEndpoint` and `PhysicalAttackEndpointExtensions` in `src/MakaMek.Core/Models/Game/Mechanics/PhysicalAttack/`.
- Replace the private `UnitEndpoint`/`GetEndpoint` in `PhysicalAttackEligibility.cs` and in `MovementAttackDeclarationValidator.cs` with the shared record and extension.
- Use the signed raw level difference in both services. Keep the Punch/Kick height adjustment, the Push equal-level check and the Charge absolute limit local.
- Add unit tests for the extension: ground in Depth 1 and Depth 2; a prone unit in Depth 1; a bridge over water; a missing hex; a map hex replaced after the unit deployed while `Unit.Hex` is non-null.
- Acceptance: all existing `PhysicalAttackEligibility` and `MovementPhase` tests pass unchanged.

### Issue B — Apply the canonical water predicate (behaviour-preserving for Charge)

- Add the shared predicate on the endpoint record, keyed by `PhysicalAttackType`, with the per-attack conditions in D4.
- Use it in `PhysicalAttackEligibility` for the existing water check. Punch/Kick/Push behaviour must not change.
- Use it in `IsChargeAllowed` and `IsDfaAllowed`. Charge keeps the symmetric rule: **do not** remove the equal-submersion check.
- Keep `HandleCommand_WhenChargingFromSubmergedWater_ShouldRejectTheCharge` rejecting the charge; it expresses the confirmed rule.
- Fix the confounded `HandleCommand_WhenTheChargeTargetIsSubmerged_ShouldRejectTheCharge` so the effective levels are equal or differ by one, isolating the water rule. Add cases for:
  - both endpoints submerged (accept),
  - a Depth 1 approach against a submerged target one level deeper (reject),
  - a submerged approach against a surface target at an equal level (reject),
  - both endpoints surface (accept),
  - a bridge over water.
- Keep the DFA target and attacker guards explicit; the shared predicate returns `false` for DFA.

### Issue C — Make the DFA guards explicit

- Keep the explicit rejection of a submerged DFA target and comment it with a reference to this document.
- Rename or comment the attacker-submersion guard as a jump-jet capability check.
- Investigate whether the DFA clearance formula must subtract the start standing level; compare with the jump ceiling calculation in `BattleMap`.
- Investigate whether `Unit.Hex` jet availability and the map-based endpoint can disagree after a hex replacement **or for a unit on a bridge over water**. `UnitPart.IsSubmerged` reads `Unit.Hex.GetWaterDepth()` without the occupied surface, so a bridge over Depth 2 water can mark torso jump jets as submerged and prevent a legal jump even when the hex has not been replaced. Keep the map-replacement case.
- Report findings before changing any behaviour.

### Issue D — Make the movement validator replaceable (optional)

- Inject `IMovementAttackDeclarationValidator` through the normal phase construction so a house rule can replace Charge/DFA validation.
- This is the separate follow-up named in D6. It is not required for the shared primitive.

### Deferred / open questions

- Underwater movement cost (4 MP per hex) and charge-distance consequences: verify against the movement code before documenting them as implemented.
- Charge/DFA resolution: underwater damage halving (#1503), charge return collision, and hull breach on charge resolution.
