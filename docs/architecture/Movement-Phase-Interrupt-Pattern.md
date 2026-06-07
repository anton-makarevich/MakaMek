# Movement Phase Interrupt Pattern

How the Movement phase evaluates terrain/movement hazards (bridge collapse, skid, water entry, jump-landing damage) without hard-coding each one into the phase. The pattern keeps `MovementPhase` thin and orchestration-only, while each hazard lives in its own self-contained handler.

## Why

A unit moving along a path can trigger hazards mid-move: it may skid off pavement on a sharp turn, plunge through a collapsing bridge, fall entering deep water, or take a piloting roll when landing from a jump. Each hazard has its own trigger condition, its own roll, and its own consequence (truncate the path, broadcast a fall, defer the next step, etc.).

Encoding all of that inline in `ProcessMoveCommand` would produce one large, branchy method that is hard to test and hard to extend. The interrupt pattern decomposes it into three roles:

- **The phase** orchestrates: it walks the path, asks each handler about each segment, and applies whatever the handler returns.
- **Interrupt handlers** decide: each one inspects a segment (or the landing hex) and decides whether *its* hazard fires, producing a result describing what should happen.
- **Game actions** execute: small command objects that mutate game state (`game.OnX(...)`) and return the commands to publish.

This is a Chain-of-Responsibility (handlers) feeding a Command pattern (actions), with the phase as the mediator.

## Components

```text
MovementPhase  ──walks path / landing──►  IMovementInterruptHandler.Check(context)
                                                       │
                                                       ▼
                                          MovementInterruptResult?  (null = no fire)
                                                       │
                                                       ▼
                              MovementPhase.ProcessInterruptResult(...)
                                                       │
                                                       ▼
                                  IGameAction.Process(game) ──► IReadOnlyList<IGameCommand>
                                                       │
                                                       ▼
                                       Game.CommandPublisher.PublishCommand(...)
```

### `IMovementInterruptHandler` — the decision

```csharp
public interface IMovementInterruptHandler
{
    MovementInterruptResult? Check(MovementInterruptContext context);
}
```

A handler evaluates **one segment for one hazard type** and returns `null` when its hazard does not apply. Handlers are pure decision logic: they read the game/map, run the relevant piloting-skill roll through `Game.FallProcessor`, and package the outcome as actions. They never publish commands or mutate state directly — that is deferred to the actions they emit.

### `MovementInterruptContext` — the input

An immutable bundle passed to every handler:

| Field | Meaning |
|-------|---------|
| `MoveCommand` | The full original `MoveUnitCommand` |
| `SegmentIndex` | Index of the segment being evaluated |
| `Unit` | The moving unit (may or may not be a `Mech`) |
| `Game` | The `ServerGame` (map lookups, `FallProcessor`, players) |
| `IsLandingCheck` | `true` when evaluating a post-jump landing rather than a per-segment walk/run interrupt |

`IsLandingCheck` lets a single handler serve both phases of evaluation (e.g. `BridgeCollapseInterruptHandler` and `WaterEntryInterruptHandler` behave differently for landing vs. walk-through).

### `MovementInterruptResult` — the output

```csharp
public bool ShouldStop { get; init; }            // stop the whole move; no further segments/handlers
public bool DeferStepConsumption { get; init; }  // only meaningful when ShouldStop
public IReadOnlyList<IGameAction> GameActions { get; init; }
```

- `ShouldStop = false` → the handler produced side effects (typically a PSR command) but movement **continues** to the next segment/handler. Used when a roll succeeds and the unit keeps going.
- `ShouldStop = true` → the move is interrupted; the phase applies the actions and stops checking.
- `DeferStepConsumption` → tells the phase to hold the unit's turn open (the unit fell and may stand up next). See *Step deferral* below.

### `IGameAction` — the execution

```csharp
public interface IGameAction
{
    IReadOnlyList<IGameCommand> Process(ServerGame game);
}
```

Each action performs a unit of state mutation against `ServerGame` and returns the commands the phase should publish. Keeping `OnX` mutation and command production together (and out of the handlers) means the phase can collect all commands and publish them in a single, ordered pass.

Concrete actions:

| Action | Mutation | Publishes |
|--------|----------|-----------|
| `MoveUnitAction(command, publish)` | `game.OnMoveUnit(command)` | the command iff `publish` |
| `BridgeCollapsedAction(command, publish)` | `game.OnBridgeCollapsed(command)` | the command iff `publish` |
| `ApplyFallAction(mech, fallCommand)` | `game.OnMechFalling` + crit hits + consciousness rolls | fall command + derived commands |
| `WaterFallBroadcastAction(mech, truncatedCommand)` | builds the post-fall broadcast, completing the move if the mech can't stand up | broadcast command |
| `PublishCommandAction(command)` | none | the command (pure broadcast) |

The `publish` flag matters for ordering: a handler can mutate state silently first (`publish: false`), interleave another action, then emit the publishable command later (`publish: true`). `BridgeCollapseInterruptHandler` uses exactly this to land the unit, collapse the bridge, then re-broadcast the completed move.

## Control flow in `MovementPhase`

`ProcessMoveCommand` runs two distinct evaluation modes:

### 1. Per-segment interrupts (walk/run)

For non-jump moves, the phase iterates every segment and, for each, runs the **segment handler chain** in order:

```csharp
private readonly IReadOnlyList<IMovementInterruptHandler> _segmentInterruptHandlers =
[
    new BridgeCollapseInterruptHandler(),
    new SkidInterruptHandler(),
    new WaterEntryInterruptHandler()
];
```

The first handler returning a `ShouldStop` result ends the move. Handlers returning `null` are skipped; handlers returning a non-stopping result (e.g. a successful PSR) emit their commands and the loop continues.

### 2. Jump landing checks

For jumps, the phase **first** completes the move normally (`OnMoveUnit` + broadcast `IsCompleted = true`), then evaluates the landing hex against the **landing handler chain**:

```csharp
private readonly IReadOnlyList<IMovementInterruptHandler> _landingInterruptHandlers =
[
    new BridgeCollapseInterruptHandler(),
    new JumpDamageInterruptHandler(),
    new WaterEntryInterruptHandler()
];
```

These run with `IsLandingCheck = true` and `SegmentIndex` pointing at the final segment.

Note the two chains share handlers but differ in membership and order: skid only applies to running, jump-landing damage only applies to landing. A handler that does not apply to its mode simply guards and returns `null` (e.g. `SkidInterruptHandler` requires `MovementType.Run`; `JumpDamageInterruptHandler` requires `Jump` + `IsLandingCheck`).

### Applying a result

```csharp
private bool ProcessInterruptResult(MovementInterruptResult result, IUnit unit)
{
    var commands = new List<IGameCommand>();
    bool? deferAfterFall = null;
    foreach (var action in result.GameActions)
    {
        commands.AddRange(action.Process(Game));
        if (unit is Mech fallingMech && action is ApplyFallAction)
            deferAfterFall = fallingMech.CanStandup();
    }

    foreach (var cmd in commands)
        Game.CommandPublisher.PublishCommand(cmd);

    if (!result.ShouldStop) return false;
    _requestDeferStepConsumption = deferAfterFall ?? result.DeferStepConsumption;
    return true;
}
```

Actions are processed in order, their commands accumulated, then published as a batch. The return value tells the segment loop whether to stop.

## Step deferral

When a mech falls mid-move it may be able to stand up and continue, so its turn must stay open. The phase coordinates this across two flags and an override of `MainGamePhase.ShouldFinalizeUnitsTurn`:

- `_requestDeferStepConsumption` — set in `ProcessInterruptResult` when a stopping interrupt fired. It is computed from `Mech.CanStandup()` after the fall (queried live, because `ApplyFallAction` has already applied damage that may change the answer), falling back to the result's declared `DeferStepConsumption`.
- `_deferredMovementUnitId` — the unit whose turn is held open.

`ShouldFinalizeUnitsTurn` consumes the request: on a deferred `MoveUnitCommand` it records the unit id and returns `false` (do not advance the per-step counter); the follow-up command for that same unit clears the deferral and lets the turn finalize.

While a unit is deferred, `HandleCommand` rejects `MoveUnitCommand`/`TryStandupCommand` for *other* units, ensuring the interrupted unit resolves first. Both flags are reset on `Enter`/`Exit` via `ClearMovementDeferralState`.

## Worked example: bridge collapse on walk

1. Unit walks onto a bridge hex whose construction factor is exceeded by the combined tonnage.
2. `BridgeCollapseInterruptHandler.Check` (walk branch) detects overweight, truncates the path at the bridge segment, tags it with `BridgeCollapse` + `Fall` events, and builds an action list:
   - `MoveUnitAction(truncatedCommand, publish: false)` — silently place the unit on the bridge hex.
   - `BridgeCollapsedAction(bridgeCmd, publish: true)` — collapse the bridge, broadcast it.
   - `MoveUnitAction(truncatedCommand with { IsCompleted = true }, publish: true)` — re-broadcast the completed move.
   - one `ApplyFallAction` per mech on the hex (each rolled through `FallProcessor`).
3. The handler returns `ShouldStop = true`.
4. `ProcessInterruptResult` runs the actions in order, publishes all commands, and — because an `ApplyFallAction` ran — sets `_requestDeferStepConsumption` from the triggering mech's post-fall `CanStandup()`.
5. The segment loop returns; the unit's turn is held open if it can stand.

## Testing

Because handlers are pure (`Check(context) → result?`) they are unit-tested in isolation, with no phase or publisher involved. `BridgeCollapseInterruptHandlerTests` constructs a `MovementInterruptContext` directly, stubs `MockFallProcessor.ProcessMovementAttempt(...)`, and asserts on the returned `MovementInterruptResult` — e.g. `ShouldStop` is true and `GameActions` contains a `BridgeCollapsedAction` and an `ApplyFallAction`. Actions and the phase orchestration are tested separately, so each layer's responsibility is verified independently.

## Adding a new hazard

1. Implement `IMovementInterruptHandler`. Guard early and return `null` when the hazard does not apply (wrong movement type, wrong segment, not a `Mech`, `IsLandingCheck` mismatch, etc.).
2. Run any roll through `context.Game.FallProcessor.ProcessMovementAttempt(...)` with the appropriate `*RollContext`.
3. Return a `MovementInterruptResult`: `ShouldStop = false` for a passed roll that only needs a PSR broadcast, `true` to interrupt the move.
4. Build the consequence from existing `IGameAction`s; add a new action only if no current one fits, keeping mutation + publish-list together in the action.
5. Register the handler in `_segmentInterruptHandlers` and/or `_landingInterruptHandlers` at the correct position in the chain.
5. Register the handler in `_segmentInterruptHandlers` and/or `_landingInterruptHandlers` at the correct position in the chain.