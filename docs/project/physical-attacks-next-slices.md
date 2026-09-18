# Physical Attack Next Slices

The initial vertical slice supports adjacent BattleMech Punch and Kick attacks. The following
features require separate contracts before production implementation.

## Slice 1 — Push (in progress)

Required decisions and data:

- Require both units to be standing, adjacent, and eligible to act.
- Determine the push direction from attacker to target and validate the destination hex.
- Define whether the target moves one hex, whether occupied destinations trigger domino displacement,
  and how map edges/blocking terrain behave.
- Add authoritative displacement output and client replay handling.
- Add PSR contexts/results for attacker and target, including fall behavior and failure ordering.

Likely implementation seams: `PhysicalAttackValidator`, a physical-attack resolution result that
can carry actions/events, existing `DisplaceUnitCommand`, movement displacement actions, and the
piloting/fall pipeline.

## Slice 2 — Charge

Required decisions and data:

- Connect the declaration to a completed movement path and movement mode.
- Validate the attacker entered the target hex legally and determine attacker/target damage.
- Define missed-charge destination, facing, displacement, PSR/DSR ordering, and fall results.
- Ensure movement state is not applied twice when the attack is resolved.

Likely implementation seams: movement command/path data, movement phase completion state, physical
resolution, damage transfer, and fall actions.

Safe preparation is complete for this slice: `Charge` remains rejected at the phase boundary, and
local serialized transport tests verify that rejection does not publish a resolution or consume
the active unit's turn. The first production change should introduce the declaration/path contract
before enabling the enum value.

## Slice 3 — Death From Above (DFA)

Required decisions and data:

- Require a jump movement path and a valid landing target.
- Define landing success, attacker/target damage, target displacement, and both PSR sequences.
- Define the attacker's post-DFA facing/position and fall behavior.

This should reuse movement/fall actions rather than adding a second jump or fall implementation.

Like Charge, DFA is covered by serialized rejection tests until its jump path and landing contract
are defined.

## Slice 4 — Physical weapons

Required decisions and data:

- Identify eligible melee components such as Hatchet and Sword.
- Define how a declaration selects a mounted component and how destroyed/removed components are
  rejected.
- Define weapon damage, location, facing, actuator modifiers, and result rendering.
- Extend the command and UI models if one unit can choose among multiple physical weapons.

The existing `Weapon`/`WeaponType.Melee` model is a starting point, but it does not by itself
define physical-attack eligibility or selection.

## Slice 5 — Piloting consequences

Required decisions and data:

- Add explicit roll contexts for physical-attack consequences.
- Define when a miss, kick, push, charge, or DFA requires a PSR/DSR.
- Reuse `IPilotingSkillCalculator`, `IFallProcessor`, and polymorphic roll serialization.
- Broadcast authoritative roll/fall outcomes in deterministic order.

## Current guardrail

Push currently has a deliberately narrow implementation: adjacent BattleMechs can make a push, a
hit displaces the target one hex directly away when that hex exists and is unoccupied, and no
domino or PSR consequences are applied yet. Charge and DFA remain rejected by
`PhysicalAttackValidator` until their movement contracts exist.

## Expansion hardening checklist

- Keep every new physical-attack result replayable through `GameCommandJsonConverter`.
- Treat displacement, damage, fall, and PSR outcomes as authoritative server data; clients should
  apply commands rather than recalculate them.
- Preserve `IdempotencyKey` values when accepted declarations are rebroadcast.
- Reject stale, duplicate, wrong-owner, same-side, unsupported, and blocked-destination commands
  before rolling dice or consuming the active unit's action.
- Add one focused Core test, one serialized transport test, and one Presentation test for each new
  attack type before exposing it in the UI or bot decision engines.
