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

## Recommended Solution

**Approach: Deferred command publishing during phase initialization**

The key insight is that we need to satisfy two requirements:
1. Server must complete `Enter()` before clients can send commands back
2. Clients must receive `ChangePhaseCommand` BEFORE any other commands (like `ChangeActivePlayerCommand`)

These requirements are contradictory if `Enter()` publishes commands immediately, because:
- If we call `Enter()` before publishing `ChangePhaseCommand`, clients receive `ChangeActivePlayerCommand` first (wrong order)
- If we publish `ChangePhaseCommand` before calling `Enter()`, clients can send commands before server is ready (race condition)

### Solution: Deferred Publishing

Implement a deferred publishing mechanism in `CommandPublisher`:

```csharp
public class CommandPublisher
{
    private Queue<IGameCommand>? _deferredCommands;

    public void BeginDefer()
    {
        _deferredCommands = new Queue<IGameCommand>();
    }

    public void PublishCommand(IGameCommand command)
    {
        if (_deferredCommands != null)
        {
            _deferredCommands.Enqueue(command);
        }
        else
        {
            Adapter.PublishCommand(command);
        }
    }

    public void EndDefer()
    {
        if (_deferredCommands == null) return;

        while (_deferredCommands.Count > 0)
        {
            var command = _deferredCommands.Dequeue();
            Adapter.PublishCommand(command);
        }
        _deferredCommands = null;
    }
}
```

Then modify `TransitionToPhase()`:

```csharp
private void TransitionToPhase(IGamePhase newPhase)
{
    if (_currentPhase is GamePhase currentGamePhase)
    {
        currentGamePhase.Exit();
    }
    _currentPhase = newPhase;

    // Set phase locally on server first
    TurnPhase = newPhase.Name;

    // Clear processed command keys when phase changes
    _processedCommandKeys.Clear();

    // Defer command publishing during Enter()
    CommandPublisher.BeginDefer();

    // Initialize the phase - commands are queued, not published
    _currentPhase.Enter();

    // Publish ChangePhaseCommand FIRST
    CommandPublisher.PublishCommand(new ChangePhaseCommand
    {
        GameOriginId = Id,
        Phase = newPhase.Name
    });

    // Then publish all deferred commands from Enter()
    CommandPublisher.EndDefer();
}
```

### Why This Works

1. **Server sets `TurnPhase` locally** - server knows the current phase
2. **Server calls `Enter()` with deferred publishing** - phase is fully initialized, commands are queued
3. **`ChangePhaseCommand` is published first** - clients update their phase, bots update decision engines
4. **Deferred commands are published** - clients receive `ChangeActivePlayerCommand`, consciousness rolls, etc.
5. **Clients receive commands in correct order**:
   - First: `ChangePhaseCommand` - clients update phase, bots update decision engines
   - Then: `ChangeActivePlayerCommand` - bots make decisions with correct engine
   - Then: Other phase initialization commands (consciousness rolls, etc.)
6. **Server is ready** - `Enter()` has completed, server can process incoming commands

### Command Ordering Example

For a transition to `MovementPhase`:
1. Server calls `BeginDefer()`
2. Server calls `MovementPhase.Enter()` → calls `SetNextPlayerActive()` → calls `SetActivePlayer()` → queues `ChangeActivePlayerCommand`
3. Server publishes `ChangePhaseCommand` (goes out immediately)
4. Server calls `EndDefer()` → publishes queued `ChangeActivePlayerCommand`
5. Clients receive: `ChangePhaseCommand`, then `ChangeActivePlayerCommand`
6. Bots update decision engine to `MovementEngine`, then make movement decisions

### Additional Consideration

We need to refactor `SetPhase()` since it currently both sets `TurnPhase` AND publishes the command. The new approach separates these concerns - `TurnPhase` is set directly in `TransitionToPhase()`, and the command is published explicitly.

## Alternative Solutions Considered

### Option 2: Two-step phase transition protocol
- Server sends "prepare phase" command, clients acknowledge, then server sends "activate phase"
- **Rejected**: Too complex, requires significant refactoring, adds latency

### Option 3: Command batching
- Batch all commands during phase transition, publish atomically
- **Rejected**: Adds complexity to `CommandPublisher`, doesn't solve the fundamental ordering issue

### Option 4: Delay bot reactions
- Add artificial delay before bots react to phase changes
- **Rejected**: Hacky, unreliable, doesn't fix the root cause

## Implementation Impact

### Files to Modify
1. `src/MakaMek.Core/Services/Transport/CommandPublisher.cs` - Add deferred publishing mechanism
   - Add `_deferredCommands` queue field
   - Add `BeginDefer()` method
   - Modify `PublishCommand()` to check for deferred mode
   - Add `EndDefer()` method

2. `src/MakaMek.Core/Models/Game/ServerGame.cs` - Modify `TransitionToPhase()`
   - Set `TurnPhase` directly instead of calling `SetPhase()`
   - Use deferred publishing around `Enter()` call
   - Explicitly publish `ChangePhaseCommand`
   - Remove or refactor `SetPhase()` method (it's no longer needed in its current form)

3. `src/MakaMek.Core/Services/Transport/ICommandPublisher.cs` - Add interface methods
   - Add `BeginDefer()` to interface
   - Add `EndDefer()` to interface

### Testing Requirements
1. **Unit tests for CommandPublisher**:
   - Test that `BeginDefer()` queues commands instead of publishing
   - Test that `EndDefer()` publishes all queued commands in order
   - Test nested defer scenarios (if applicable)
   - Test that normal publishing works when not in defer mode

2. **Integration tests for phase transitions**:
   - Test that `ChangePhaseCommand` arrives before `ChangeActivePlayerCommand`
   - Test that bots receive phase change before active player change
   - Test that server completes `Enter()` before processing bot commands

3. **Phase-specific tests**:
   - Test `EndPhase` properly clears `_playersEndedTurn` before processing `TurnEndedCommand`
   - Test `MainGamePhase` sets active player before processing movement/attack commands
   - Test `DeploymentPhase` sets deployment order before processing deploy commands

4. **Bot behavior tests**:
   - Test that bots use correct decision engine when making decisions
   - Test that bots don't send commands with wrong decision engine
   - Test rapid phase transitions with multiple bots

### Thread Safety Considerations

The deferred publishing mechanism should be thread-safe if commands can be published from multiple threads. Consider using a `ConcurrentQueue<IGameCommand>` instead of `Queue<IGameCommand>` if needed, or ensure that `BeginDefer()`, `PublishCommand()`, and `EndDefer()` are always called from the same thread (which is likely the case for phase transitions).

## Conclusion

The recommended solution uses deferred command publishing to ensure:
1. The server completes phase initialization (`Enter()`) before clients can react
2. Clients receive `ChangePhaseCommand` before any other phase-related commands
3. Bots update their decision engines before receiving active player changes
4. Command ordering is deterministic and correct

This approach is more complex than simple reordering, but it's necessary to satisfy both the initialization requirement and the command ordering requirement. The implementation is clean, testable, and doesn't require changes to phase classes themselves.

