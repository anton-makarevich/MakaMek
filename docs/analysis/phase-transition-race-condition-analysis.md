# Phase Transition Race Condition Analysis

## Problem Description

There is a race condition in the phase transition logic in `ServerGame.TransitionToPhase()` that can cause bots and clients to send commands before the server has fully initialized the new phase.

## Current Implementation

In `ServerGame.TransitionToPhase()` (line 84-93):

```csharp
private void TransitionToPhase(IGamePhase newPhase)
{
    if (_currentPhase is GamePhase currentGamePhase)
    {
        currentGamePhase.Exit();
    }
    _currentPhase = newPhase;
    SetPhase(newPhase.Name);      // Step 3: Publishes ChangePhaseCommand to clients
    _currentPhase.Enter();         // Step 4: Initializes phase on server
}
```

### The Race Condition

**Timeline of events:**

1. **Server**: Calls `SetPhase()` which publishes `ChangePhaseCommand` to all clients
2. **Clients**: Receive `ChangePhaseCommand` immediately
3. **Clients**: Set `TurnPhase` property, triggering phase change notifications
4. **Bots**: Receive phase change notification via `PhaseChanges` observable
5. **Bots**: Update decision engine for new phase
6. **Clients**: For `EndPhase`, set `ActivePlayer` locally (line 106 in `ClientGame.cs`)
7. **Bots**: Receive active player change notification
8. **Bots**: Start making decisions and sending commands back to server
9. **Server**: Still hasn't called `phase.Enter()` yet! ⚠️

**The problem**: Bots can send commands (e.g., `StartupUnitCommand`, `ShutdownUnitCommand`, `TurnEndedCommand`) before the server has completed phase initialization via `Enter()`.

## Specific Issues by Phase

### EndPhase
`EndPhase.Enter()` (line 13-23):
- Clears `_playersEndedTurn` HashSet
- Processes consciousness recovery rolls
- Checks victory conditions

**Issue**: If a bot sends `TurnEndedCommand` before `_playersEndedTurn` is cleared, the HashSet might contain stale data from the previous turn, causing incorrect "all players ended turn" detection.

### MainGamePhase (Movement, WeaponsAttack, PhysicalAttack)
`MainGamePhase.Enter()` (line 15-19):
- Calculates turn order from initiative
- Calls `SetNextPlayerActive()` which sets the active player and publishes `ChangeActivePlayerCommand`

**Issue**: If a bot sends a command before `SetNextPlayerActive()` is called, the server's `ActivePlayer` might be null or incorrect, causing command validation to fail (line 36 checks `playerId == Game.ActivePlayer?.Id`).

### DeploymentPhase
`DeploymentPhase.Enter()` (line 11-15):
- Randomizes deployment order
- Sets next deploying player

**Issue**: If a bot sends `DeployUnitCommand` before deployment order is established, the `ActivePlayer` won't be set correctly.

## Why Simple Reordering Doesn't Work

One might think: "Just call `Enter()` before `SetPhase()`". However, this creates a different race condition:

**If we call `Enter()` before `SetPhase()`:**
1. Server calls `newPhase.Enter()`
2. For server-driven phases, `Enter()` calls `SetActivePlayer()`, which publishes `ChangeActivePlayerCommand`
3. Clients receive `ChangeActivePlayerCommand` while still in the old phase
4. Bots receive active player change notification
5. Bots call `MakeDecision()` with the OLD decision engine (e.g., `WeaponsEngine` instead of `MovementEngine`)
6. Server publishes `ChangePhaseCommand`
7. Clients update phase, bots update decision engine - but they already made a wrong decision!

**Critical insight**: Bots subscribe to both `PhaseChanges` and `ActivePlayerChanges`. They need to receive `ChangePhaseCommand` FIRST to update their decision engine, THEN receive `ChangeActivePlayerCommand` to make decisions with the correct engine.

## Command Ordering Requirements

For correct operation:
1. `ChangePhaseCommand` must arrive at clients FIRST
2. Then `ChangeActivePlayerCommand` (and any other phase initialization commands)
3. Server must complete `Enter()` before processing any commands from clients/bots

## Implemented Solution

**Approach: Two-Stage Phase Transition Protocol with StartPhaseCommand**

The key insight is that we need to satisfy two requirements:
1. Server must complete `Enter()` before clients can send commands back
2. Clients must not trigger bot decision-making until server is ready

The solution uses a two-stage protocol:
- **Stage 1**: Server publishes `ChangePhaseCommand` and calls `Enter()` (as before)
- **Stage 2**: Server publishes `StartPhaseCommand` at the END of `Enter()` to signal completion

### Solution: StartPhaseCommand

Introduce a new `StartPhaseCommand` that signals when phase initialization is complete:

**1. Create `StartPhaseCommand`** (in `src/MakaMek.Core/Data/Game/Commands/Server/StartPhaseCommand.cs`):

```csharp
public record struct StartPhaseCommand : IGameCommand
{
    public required PhaseNames Phase { get; init; }
    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }

    public string Render(ILocalizationService localizationService, IGame game)
    {
        var localizedTemplate = localizationService.GetString("Command_StartPhase");
        return string.Format(localizedTemplate, Phase);
    }
}
```

**2. Modify `EndPhase.Enter()`** to publish `StartPhaseCommand` at the end:

```csharp
public override void Enter()
{
    // Clear the set of players who have ended their turn
    _playersEndedTurn.Clear();

    // Process consciousness recovery rolls for unconscious pilots
    ProcessConsciousnessRecoveryRolls();

    // Check for victory conditions
    CheckVictoryConditions();

    // Publish StartPhaseCommand to signal that phase initialization is complete
    Game.CommandPublisher.PublishCommand(new StartPhaseCommand
    {
        GameOriginId = Game.Id,
        Phase = PhaseNames.End
    });
}
```

**3. Update `ClientGame.HandleCommand()`** to handle `StartPhaseCommand`:

```csharp
case ChangePhaseCommand phaseCommand:
    TurnPhase = phaseCommand.Phase;

    // When entering the End phase, clear the players who ended turn
    // Note: ActivePlayer is set when StartPhaseCommand is received to avoid race condition
    if (phaseCommand.Phase == PhaseNames.End)
    {
        _playersEndedTurn.Clear();
    }
    break;

case StartPhaseCommand startPhaseCommand:
    // When the End phase is fully initialized on the server, set the first alive local player as active
    // This ensures the server has completed phase initialization before bots start making decisions
    if (startPhaseCommand.Phase == PhaseNames.End)
    {
        ActivePlayer = AlivePlayers.FirstOrDefault(p => _localPlayers.ContainsKey(p.Id));
    }
    break;
```

### Why This Works

1. **Server publishes `ChangePhaseCommand`** (as before) - clients update their phase
2. **Server calls `Enter()`** - phase is fully initialized (clears state, processes consciousness rolls, etc.)
3. **Server publishes `StartPhaseCommand`** at the end of `Enter()` - signals initialization is complete
4. **Clients receive `ChangePhaseCommand`** - set `TurnPhase`, but DON'T set `ActivePlayer` yet for EndPhase
5. **Clients receive other commands** from `Enter()` (consciousness rolls, etc.)
6. **Clients receive `StartPhaseCommand`** - NOW set `ActivePlayer` for EndPhase
7. **Bots receive `ActivePlayer` change** - make decisions with correct decision engine
8. **Server is ready** - `Enter()` has completed, server can process incoming commands

### Command Flow Example

For a transition to `EndPhase`:

**Server side:**
1. `TransitionToPhase()` calls `SetPhase()` → publishes `ChangePhaseCommand`
2. `TransitionToPhase()` calls `EndPhase.Enter()`
3. `Enter()` clears `_playersEndedTurn`
4. `Enter()` calls `ProcessConsciousnessRecoveryRolls()` → publishes `PilotConsciousnessRollCommand` (if any)
5. `Enter()` calls `CheckVictoryConditions()` → might publish `GameEndedCommand`
6. `Enter()` publishes `StartPhaseCommand` at the end
7. Server is now ready to process commands

**Client side:**
1. Receives `ChangePhaseCommand` → sets `TurnPhase = PhaseNames.End`, clears `_playersEndedTurn`
2. Receives `PilotConsciousnessRollCommand` (if any) → updates pilot consciousness state
3. Receives `StartPhaseCommand` → sets `ActivePlayer` to first alive local player
4. Bots receive `ActivePlayer` change → call `MakeDecision()` with `EndPhaseEngine`
5. Bots send commands (`StartupUnitCommand`, `ShutdownUnitCommand`, `TurnEndedCommand`)
6. Server processes commands (phase is fully initialized)

### Scope

This solution is currently implemented **only for EndPhase** as a focused fix for the most critical race condition. The same pattern can be extended to other phases (MainGamePhase, DeploymentPhase) if needed in the future.

## Alternative Solutions Considered

### Option 1: Deferred command publishing
- Implement a deferred publishing mechanism in `CommandPublisher` to queue commands during `Enter()`
- Publish `ChangePhaseCommand` first, then publish all queued commands
- **Rejected**: More complex than the two-stage protocol, requires changes to `CommandPublisher` infrastructure
- **Issue**: The RX transport processes commands synchronously, so even with deferred publishing, clients could react before the server finishes `Enter()`

### Option 2: Simple reordering (call Enter() before SetPhase())
- Call `Enter()` before publishing `ChangePhaseCommand`
- **Rejected**: Creates a different race condition where `ChangeActivePlayerCommand` arrives before `ChangePhaseCommand`
- **Issue**: Bots would react to active player change with the wrong decision engine

### Option 3: Delay bot reactions
- Add artificial delay before bots react to phase changes
- **Rejected**: Hacky, unreliable, doesn't fix the root cause

## Implementation Impact

### Files Modified
1. **`src/MakaMek.Core/Data/Game/Commands/Server/StartPhaseCommand.cs`** - NEW
   - Created new command to signal phase initialization completion
   - Follows same pattern as `ChangePhaseCommand`

2. **`src/MakaMek.Core/Models/Game/Phases/EndPhase.cs`** - MODIFIED
   - Added `StartPhaseCommand` publishing at the end of `Enter()` method
   - Signals that phase initialization is complete

3. **`src/MakaMek.Core/Models/Game/ClientGame.cs`** - MODIFIED
   - Added handler for `StartPhaseCommand`
   - Moved `ActivePlayer` setting for EndPhase from `ChangePhaseCommand` handler to `StartPhaseCommand` handler
   - Added comment explaining the race condition prevention

4. **`tests/MakaMek.Core.Tests/Models/Game/Phases/EndPhaseTests.cs`** - MODIFIED
   - Added test `Enter_ShouldPublishStartPhaseCommand_WhenPhaseInitializationCompletes`
   - Added test `Enter_ShouldPublishStartPhaseCommandAfterOtherInitialization`
   - Verifies that `StartPhaseCommand` is published with correct properties
   - Verifies that `StartPhaseCommand` is published last (after consciousness rolls, etc.)

### Testing Results
All 23 EndPhase tests pass, including the new tests for `StartPhaseCommand`:
- ✅ `Enter_ShouldPublishStartPhaseCommand_WhenPhaseInitializationCompletes`
- ✅ `Enter_ShouldPublishStartPhaseCommandAfterOtherInitialization`

### Future Extensions

This solution is currently implemented only for `EndPhase`. If the race condition becomes an issue for other phases, the same pattern can be applied:

**For MainGamePhase (Movement, WeaponsAttack, PhysicalAttack):**
- Modify `MainGamePhase.Enter()` to publish `StartPhaseCommand` at the end
- Update `ClientGame.HandleCommand()` to handle `StartPhaseCommand` for these phases
- Consider whether `ChangeActivePlayerCommand` should be deferred until after `StartPhaseCommand`

**For DeploymentPhase:**
- Modify `DeploymentPhase.Enter()` to publish `StartPhaseCommand` at the end
- Update `ClientGame.HandleCommand()` to handle `StartPhaseCommand` for deployment
- Consider whether deployment order randomization should be signaled to clients

## Conclusion

The implemented two-stage phase transition protocol using `StartPhaseCommand` successfully resolves the race condition for `EndPhase`:

1. ✅ **Server completes initialization** - `Enter()` finishes before clients set `ActivePlayer`
2. ✅ **Bots react at the right time** - `ActivePlayer` change happens after `StartPhaseCommand`
3. ✅ **Simple and focused** - Only 3 files modified, minimal complexity
4. ✅ **Well tested** - 2 new tests verify correct behavior
5. ✅ **Extensible** - Same pattern can be applied to other phases if needed

This approach is simpler than deferred publishing, doesn't require infrastructure changes, and directly addresses the root cause of the race condition by delaying bot activation until the server is ready.

