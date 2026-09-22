# Physical Attacks — Initial Implementation Plan

> Status: **planned.** The punch/kick vertical slice is the proposed scope; none of it is on `main`
> yet. Push, charge, DFA, and physical-weapon attacks stay out of scope until their movement,
> displacement, PSR/DSR, equipment, and damage-transfer rules have dedicated acceptance criteria.

## Status

Proposed vertical slice, not yet implemented. `PhysicalAttackPhase` exists in the codebase but is
not wired into `PhaseManager`, so nothing reaches it during a game. Once the slice lands the phase
order becomes `WeaponAttackResolution → PhysicalAttack → Heat`.

The slice covers adjacent BattleMech punch and kick declarations, authoritative resolution, damage
result broadcasting, and explicit pass handling. Push, charge, DFA, physical weapons, vehicles, and
advanced physical-attack rules remain future slices.

## Goal

Add a small, testable physical-attack slice for BattleMechs without silently changing the Version 1 rules or making a match wait for UI actions that do not exist yet.

## First slice

The first playable slice supports:

- Punch attacks against an adjacent opposing BattleMech.
- Kick attacks against an adjacent opposing BattleMech.
- One physical-attack declaration per unit per turn.
- Server-side validation of ownership, phase, adjacency, target validity, and attack eligibility.
- Deterministic command/result data that clients can render and replay.
- Explicit pass/no-attack handling so the phase cannot deadlock.

The first slice does not include push, charge, DFA, clubs, physical weapons, vehicles, or advanced
technology. Push needs its own displacement, domino, and PSR acceptance criteria before it ships.

## Existing seams

- `PhysicalAttackCommand` provides the client-to-server declaration shape.
- `PhysicalAttackPhase` owns command authorization and per-unit turn progression.
- `PhysicalAttackPhase` is the server resolution seam and publishes both the accepted declaration
  and `PhysicalAttackResolutionCommand`.
- `IToHitCalculator` and the existing damage-transfer services are reused instead of adding a
  parallel combat pipeline.
- `PhaseNames.PhysicalAttack` is registered by `BattleTechPhaseManager` between weapon resolution
  and heat.

## Remaining contracts and follow-up slices

1. Add Push with its displacement, domino, and PSR rules, then result data and rules for charge,
   DFA, physical weapons, and piloting consequences.
2. Define the complete punch/kick modifiers for actuator damage and facing as the rules slice grows.
3. Add presentation coverage for target selection, explicit Punch/Kick actions, pass, and result
   display.
4. Add invalid, duplicate, stale, and rejected-command transport cases.

Detailed acceptance planning for the next rule slices is in
`docs/project/physical-attacks-next-slices.md`. Push, charge, DFA, physical weapons, and PSR
consequences remain intentionally out of scope until their movement, displacement, equipment, and
roll-result contracts are complete.

## Acceptance criteria

- A unit can complete the physical-attack phase by attacking or passing.
- Invalid commands do not mutate state or advance the active-unit counter.
- A valid attack is resolved exactly once and is broadcast with its authoritative result.
- Replayed client state produces the same visible result as the server state.
- Existing Version 1 documentation remains a simplified baseline; this vertical slice is intended
  to be enabled by default once it lands.

## Decisions still needed from the creator

- Whether the next physical-attack slice should use the broader rules in `docs/rules/Overview.md` or
  remain a narrower MVP interpretation.
- Whether a miss or kick should trigger a piloting skill roll in the next slice.
- Whether a rules/profile option is needed when advanced physical-attack variants are introduced.
