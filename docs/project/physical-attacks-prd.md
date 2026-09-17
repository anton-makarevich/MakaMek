# Physical Attacks — Initial Implementation Plan

## Status

Proposed first vertical slice. Physical attacks are intentionally not wired into the live phase order yet. The current Version 1 rules document explicitly excludes them, and the existing command and phase are placeholders.

## Goal

Add a small, testable physical-attack slice for BattleMechs without silently changing the Version 1 rules or making a match wait for UI actions that do not exist yet.

## First slice

The first playable slice should support:

- Punch attacks against an adjacent opposing BattleMech.
- Kick attacks against an adjacent opposing BattleMech.
- One physical-attack declaration per unit per turn.
- Server-side validation of ownership, phase, adjacency, target validity, and attack eligibility.
- Deterministic command/result data that clients can render and replay.
- Explicit pass/no-attack handling so the phase cannot deadlock.

The first slice should not include charge, DFA, push, clubs, physical weapons, vehicles, or advanced technology. Those mechanics introduce movement, falling, equipment, or unit-type rules that need separate acceptance criteria.

## Existing seams

- `PhysicalAttackCommand` already provides the client-to-server declaration shape.
- `PhysicalAttackPhase` already owns command authorization and per-unit turn progression.
- `BaseGame.OnPhysicalAttack` is currently a no-op and is the server resolution seam.
- `IToHitCalculator` and the existing damage-transfer services should be reused instead of adding a parallel combat pipeline.
- `PhaseNames.PhysicalAttack` already exists, but `BattleTechPhaseManager` currently skips it.
- Presentation should receive a dedicated physical-attack UI state before the phase is inserted into the live flow.

## Required contracts before phase wiring

1. Define the physical-attack result/event payload, including hit or miss, damage, hit location, and any required piloting roll.
2. Define how a unit passes without attacking.
3. Define the exact punch and kick modifiers, including arm/leg actuator damage and attacker/target facing.
4. Define how damage is applied atomically and broadcast to clients.
5. Add server and client replay tests for valid, invalid, duplicate, stale, and pass commands.
6. Add Presentation tests for selecting a target, choosing punch/kick, passing, and displaying results.

## Acceptance criteria

- A unit can complete the physical-attack phase by attacking or passing.
- Invalid commands do not mutate state or advance the active-unit counter.
- A valid attack is resolved exactly once and is broadcast with its authoritative result.
- Replayed client state produces the same visible result as the server state.
- Existing Version 1 games remain unchanged until the complete slice is enabled deliberately.

## Decisions needed from the creator

- Whether the initial physical-attack slice should use the simplified rules in `docs/rules/Overview.md` or a narrower MVP interpretation.
- Whether physical attacks belong before or after weapon-attack resolution in the intended rules flow.
- Whether a miss or kick should trigger a piloting skill roll in the first slice.
- Whether physical attacks should be enabled by default once implemented or behind a rules/profile option.
