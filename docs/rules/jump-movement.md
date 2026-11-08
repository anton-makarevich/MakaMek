# Jump Movement, Jump Height and DFA LOS

This document records the jump rules implemented in MakaMek and the decisions taken where
Total Warfare constrains the outcome but not the intermediate geometry.

## Core jump rules

- A jumping unit follows a **shortest route** to its destination (fewest hexes).
- Each crossed hex costs **1 Jumping MP**, regardless of terrain, elevation or facing.
- A unit with at least **1 Jumping MP** may jump down any number of levels.
- The legal route must satisfy the jump-height ceiling below.

> If this path crosses a level higher than the sum of the unit's Jumping MP plus the level of the
> hex in which the jump started, then the unit cannot make the jump.
>
> — Total Warfare p. 53, *Jumping*

Implementation: `BattleMap.FindJumpPath` and `BattleMap.GetJumpReachableHexes` share the rule

```text
referenceLevel(hex) <= startLevel + availableJumpingMP
```

`availableJumpingMP` is the requested movement budget, not the path cost. `startLevel` is the
unit's **standing level** in the start hex, not the raw hex level:

| Start surface | Start level |
|---|---|
| Plain ground | `Hex.Level` |
| Bridge deck | `Hex.Level + bridge height` |
| Water (ground surface) | the riverbed, `Hex.Level - water depth` |

The start hex itself is not checked. The destination is part of the crossed path and **is** checked.

### Choosing among equally short routes

When several shortest routes exist, the player may choose one. MakaMek does not expose that choice
for ordinary jumps: pathfinding returns the **first legal shortest route** it finds and stops.
This is deterministic for a given map and movement budget, and it avoids enumerating every
equally short route (a distance-8 jump can have dozens).

For UI reachability, `GetJumpReachableHexes` runs one breadth-first search through hexes that clear
the ceiling and includes a destination only when its legal search distance equals its geometric
distance. A longer detour around a too-high hex therefore does not make the destination reachable.

## Reference levels (what a jump must clear)

| Hex | Level to clear |
|---|---|
| Plain terrain | `Hex.Level` |
| Light or heavy woods | `Hex.Level` — **tree height is ignored** |
| Water | the **water surface** (`Hex.Level`), never the bottom |
| Bridge | the **bridge deck** (`Hex.Level + bridge height`) when it is the top surface |
| Building | roof level — **deferred** until building terrain exists |

The helper is `HexExtensions.GetReferenceLevel()`. Building roof levels join this helper with the
building-terrain change.

### Start level

A unit standing on a bridge starts from the **bridge deck** (`Hex.Level + bridge height`). A unit
in water starts from the **riverbed** (`Hex.Level - water depth`), not the water surface. This is
the same standing-level rule used elsewhere in movement and LOS (`Hex.GetStandingLevel`).

Crossing clearance is unchanged: when a jump passes over a water hex, the level it must clear is
still the water surface, not the bottom.

## The vertical profile

`HexPosition.AirborneLevel` is the unit's **base level**, not its LOS top. It is `null` for a
grounded position.

For a jump path `p0 … pn`:

- `p0` and `pn` are grounded (`null`).
- The last translation's `From` (`p(n-1)`, the *approach* position) is always a hard waypoint:

  ```text
  AirborneLevel(approach) = max(referenceLevel(approach), referenceLevel(destination))
  ```

  This is calculated for every jump, not only a DFA. An adjacent jump has no intermediate
  position, so its start position carries the waypoint instead.
- Other intermediate levels interpolate linearly from the start level (the unit's standing level
  in `p0`) to the approach waypoint, round **up** (`Math.Ceiling`), and are clamped to
  `[referenceLevel(p_i), startLevel + availableJumpingMP]`.
- A route whose approach waypoint exceeds the ceiling is rejected (defensive guard). The
  exception is an adjacent jump: it has no crossed hex before the destination, so only the
  destination must clear `startLevel + availableJumpingMP`. A bridge deck above the unit, or the
  water surface it stands below, therefore does not block a legal adjacent jump. The stored
  waypoint on the start position still uses the reference levels, so adjacent DFA LOS is unchanged.

The same profile is used for path validity (the ceiling) and DFA line of sight. Presentation does
not derive a second jump curve.

## Death From Above (DFA)

DFA declares the target hex as the jump destination and uses the same shortest-route and jump-height
rules. It has an additional clearance requirement, implemented in
`MovementAttackDeclarationValidator`: the attacker needs enough Jumping MP to clear the target hex
level plus the target's height (a standing 'Mech is height 2), even though reaching the target hex
costs the usual 1 MP per hex.

During the Weapon Attack Phase the DFA attacker waits in the approach hex
(`MovementPathExtensions.GetStopPosition`). Its LOS position is the stored profile value:

```text
effective LOS level = Position.AirborneLevel + unit.Height
```

Because `Position.AirborneLevel` already equals
`max(referenceLevel(target), referenceLevel(approach))`, the LOS level is two levels above the
higher hex for a standing 'Mech, independent of which unit is shooting. LOS consumers pass
`Position.AirborneLevel` through the optional base-level parameters of `IBattleMap.GetLineOfSight`;
they must not derive a second DFA level.

The airborne value is cleared when the pending movement attack ends: on `ResetTurnState` and when
the attacker is destroyed. The DFA stop position itself is unchanged (issue #1681).

## Presentation

MakaMek renders a top-down 2D board, so the stored altitude is not drawn as a vertical sprite
offset. The profile exists for path validity and LOS; movement playback stays planar.

## Serialization and caching

- `HexPositionData` carries an optional `int? AirborneLevel`; older payloads without it
  deserialize to `null`.
- The value is derived from the path and map. It is not part of `MoveUnitCommand.GetPayloadHash`.
- Jump paths are cached by endpoints, jump movement type, max level change, unit height and the
  jump MP budget, because the budget changes the ceiling and therefore the selected route.

## Out of scope

- Building terrain and roof levels.
- Non-jump airborne units (VTOL, WiGE, aerospace).
- Server-side re-validation of submitted jump routes or profiles.
- DFA physical attack resolution (tracked separately).
