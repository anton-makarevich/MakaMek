# Physical Attack Next Slices

The initial vertical slice supports adjacent BattleMech Punch and Kick attacks. The following
features require separate contracts before production implementation.

## Slice 1 — Push

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

## Slice 3 — Death From Above (DFA)

Required decisions and data:

- Require a jump movement path and a valid landing target.
- Define landing success, attacker/target damage, target displacement, and both PSR sequences.
- Define the attacker's post-DFA facing/position and fall behavior.

This should reuse movement/fall actions rather than adding a second jump or fall implementation.

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

`PhysicalAttackType.Push`, `Charge`, and `DFA` exist in the enum for roadmap compatibility but are
intentionally rejected by `PhysicalAttackValidator` until these contracts are implemented. This
prevents clients from silently sending partially supported attacks.
