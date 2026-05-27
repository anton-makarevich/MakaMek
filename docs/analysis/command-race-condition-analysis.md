# Command Race Condition Analysis

related to https://github.com/anton-makarevich/MakaMek/issues/570

## Executive Summary

This document analyzes the race condition vulnerability in the MakaMek client-server command processing flow, where clients can send multiple commands before the server processes and responds to the first command. The analysis covers the current implementation, identifies specific vulnerabilities, and provides architectural recommendations for solving the problem.

## Current Implementation Overview

### Command Flow Architecture

The MakaMek game uses a command-based architecture with the following flow:

```
Client UI (WeaponsAttackState) 
  → ClientGame.DeclareWeaponAttack() 
  → CommandPublisher.PublishCommand() 
  → Transport Layer (SignalR/RxTransport)
  → ServerGame.HandleCommand() 
  → WeaponsAttackPhase.HandleCommand() 
  → MainGamePhase.HandleUnitAction() 
  → BaseGame.OnWeaponsAttack() 
  → Unit.DeclareWeaponAttack()
  → Broadcast to all clients
```

### Key Components

#### 1. Client-Side Command Publishing (`WeaponsAttackState.cs`)

**Location**: Lines 601-657

The `ConfirmWeaponSelections()` method:
- Creates a `WeaponAttackDeclarationCommand`
- Calls `_game.DeclareWeaponAttack(command)` 
- **Immediately** resets local UI state (lines 644-656)
- Does NOT wait for server acknowledgment

**Critical Issue**: The client resets its state optimistically, assuming the command will be processed. There's no mechanism to prevent sending another command for the same unit.

#### 2. Client-Side Command Sending (`ClientGame.cs`)

**Location**: Lines 193-205

The `SendPlayerAction<T>()` method:
- Only checks `CanActivePlayerAct` (line 195)
- Publishes command immediately (line 196)
- No queuing or pending command tracking

**Critical Issue**: `CanActivePlayerAct` only checks if the active player is local and can act, but doesn't track pending commands.

#### 3. Client-Side Unit State Check (`WeaponsAttackState.cs`)

**Location**: Lines 82-84, 138-139

The UI prevents selecting units that have `HasDeclaredWeaponAttack == true`:
```csharp
if (unit.HasDeclaredWeaponAttack) return;
```

**Critical Issue**: This flag is only set when the server broadcasts the command back to clients. During the network round-trip time, the flag is still `false`, allowing duplicate commands.

#### 4. Server-Side Command Processing (`WeaponsAttackPhase.cs`)

**Location**: Lines 17-19

```csharp
case WeaponAttackDeclarationCommand attackCommand:
    HandleUnitAction(command, attackCommand.PlayerId);
    break;
```

**Critical Issue**: No idempotency check. The server processes every command it receives.

#### 5. Server-Side Unit Action Handler (`MainGamePhase.cs`)

**Location**: Lines 34-47

```csharp
protected void HandleUnitAction(IGameCommand command, Guid playerId)
{
    if (playerId != Game.ActivePlayer?.Id) return;  // Only validates player
    
    ProcessCommand(command);
    
    _remainingUnits--;
    if (_remainingUnits <= 0)
    {
        SetNextPlayerActive();
        return;
    }
    Game.SetActivePlayer(Game.ActivePlayer, _remainingUnits);
}
```

**Critical Issue**: Only validates that the command is from the active player. Doesn't check if the unit has already declared an attack.

#### 6. Unit State Management (`Unit.cs`)

**Location**: Lines 404-416

```csharp
public void DeclareWeaponAttack(List<WeaponTargetData> weaponTargets)
{
    if (!IsDeployed)
    {
        throw new InvalidOperationException("Unit is not deployed.");
    }
    
    // Validate and store weapon targets
    _weaponTargets.Clear();
    _weaponTargets.AddRange(weaponTargets);
    
    HasDeclaredWeaponAttack = true;
}
```

**Critical Issue**: No check for `HasDeclaredWeaponAttack` before setting it. The method is NOT idempotent - calling it twice will overwrite the first attack declaration.

### Command Validation

**Location**: `BaseGame.cs`, lines 433-463

Current validation for `WeaponAttackDeclarationCommand`:
```csharp
WeaponAttackDeclarationCommand=> true,
```

**Critical Issue**: Auto-validates without any business logic checks. No validation for:
- Whether the unit has already declared an attack
- Whether the attacker ID is valid
- Whether the unit belongs to the player
- Whether it's the correct phase

## Race Condition Scenarios

### Scenario 1: Rapid Double-Click

**Timeline**:
1. T+0ms: User clicks "Declare Attack" button
2. T+0ms: Client sends `WeaponAttackDeclarationCommand` (Unit A, Target X)
3. T+1ms: Client resets UI state, `HasDeclaredWeaponAttack` still `false` locally
4. T+2ms: User double-clicks, selects Unit A again (allowed because flag is still false)
5. T+3ms: Client sends second `WeaponAttackDeclarationCommand` (Unit A, Target Y)
6. T+50ms: Server receives first command, processes it, broadcasts back
7. T+51ms: Server receives second command, processes it (overwrites first attack!)
8. T+100ms: Client receives first broadcast, sets `HasDeclaredWeaponAttack = true`
9. T+101ms: Client receives second broadcast, overwrites attack data

**Result**: Unit A's attack declaration is changed from Target X to Target Y, and the unit count is decremented twice, potentially skipping other units.

### Scenario 2: Network Latency

**Timeline**:
1. T+0ms: Client sends command for Unit A
2. T+0ms: Client resets UI optimistically
3. T+100ms: High network latency, no response yet
4. T+100ms: User thinks command failed, selects Unit A again
5. T+101ms: Client sends duplicate command
6. T+200ms: Server processes both commands

**Result**: Same as Scenario 1.

### Scenario 3: Multiple Units in Quick Succession

**Timeline**:
1. T+0ms: Client sends command for Unit A
2. T+1ms: Client sends command for Unit B  
3. T+2ms: Client sends command for Unit C
4. T+50ms: Server receives and processes all three
5. T+51ms: `_remainingUnits` decremented three times
6. T+52ms: Phase transitions prematurely, skipping remaining units

**Result**: Game state corruption, units don't get their turn.

## Current Safeguards (Insufficient)

### 1. Client-Side UI Check
- **Location**: `WeaponsAttackState.cs`, lines 84, 138
- **Mechanism**: Checks `unit.HasDeclaredWeaponAttack`
- **Limitation**: Only effective AFTER server broadcasts command back
- **Race Window**: Entire network round-trip time (typically 10-200ms)

### 2. Active Player Check
- **Location**: `MainGamePhase.cs`, line 36
- **Mechanism**: `if (playerId != Game.ActivePlayer?.Id) return;`
- **Limitation**: Only prevents commands from non-active players
- **Does NOT prevent**: Multiple commands from the same player for the same unit

### 3. Phase Validation
- **Location**: `ServerGame.HandleCommand()`, line 113
- **Mechanism**: Commands delegated to current phase
- **Limitation**: Phase only checks command type, not unit state

## Architectural Analysis

### Current Architecture Weaknesses

1. **No Command Acknowledgment**: Fire-and-forget pattern with no ACK/NACK
2. **Optimistic UI Updates**: Client assumes success before server confirmation
3. **No Pending Command Tracking**: No record of in-flight commands
4. **No Server-Side Idempotency**: Commands processed multiple times
5. **Weak Validation**: Auto-validation without business logic checks
6. **No Command Sequencing**: No sequence numbers or timestamps to detect duplicates

### Transport Layer Analysis

**Location**: `CommandPublisher.cs`, `CommandTransportAdapter.cs`

The transport layer is a simple publish-subscribe pattern:
- Commands are serialized and published immediately
- No queuing mechanism
- No delivery guarantees
- No duplicate detection
- No ordering guarantees (though SignalR provides ordering)

## Recommended Solutions

### Solution 1: Server-Side Idempotency (RECOMMENDED)

**Approach**: Make the server reject duplicate commands for units that have already acted.

**Implementation**:

1. **Add validation in `BaseGame.ValidateCommand()`**:
```csharp
WeaponAttackDeclarationCommand attackCommand => ValidateWeaponAttackCommand(attackCommand),
```

2. **Implement validation method**:
```csharp
private bool ValidateWeaponAttackCommand(WeaponAttackDeclarationCommand command)
{
    var player = _players.FirstOrDefault(p => p.Id == command.PlayerId);
    if (player == null) return false;
    
    var unit = player.Units.FirstOrDefault(u => u.Id == command.AttackerId);
    if (unit == null) return false;
    
    // Idempotency check: reject if unit already declared attack
    if (unit.HasDeclaredWeaponAttack) return false;
    
    return true;
}
```

3. **Make `Unit.DeclareWeaponAttack()` idempotent**:
```csharp
public void DeclareWeaponAttack(List<WeaponTargetData> weaponTargets)
{
    if (!IsDeployed)
    {
        throw new InvalidOperationException("Unit is not deployed.");
    }
    
    // Idempotency: if already declared, ignore (or throw exception)
    if (HasDeclaredWeaponAttack)
    {
        return; // Silently ignore duplicate
        // OR: throw new InvalidOperationException("Unit has already declared weapon attack.");
    }
    
    _weaponTargets.Clear();
    _weaponTargets.AddRange(weaponTargets);
    
    HasDeclaredWeaponAttack = true;
}
```

**Pros**:
- Simple to implement
- Server is source of truth
- Works with existing architecture
- No client changes required
- Handles all race condition scenarios

**Cons**:
- Client might send invalid commands (wasted bandwidth)
- No user feedback for rejected commands (unless we add error responses)

### Solution 2: Client-Side Pending Command Tracking

**Approach**: Track pending commands on the client and prevent sending duplicates.

**Implementation**:

1. **Add pending commands set to `ClientGame`**:
```csharp
private readonly HashSet<Guid> _pendingWeaponAttackUnits = [];
```

2. **Modify `DeclareWeaponAttack()`**:
```csharp
public void DeclareWeaponAttack(WeaponAttackDeclarationCommand command)
{
    if (!CanActivePlayerAct) return;
    
    // Check if command is already pending
    if (_pendingWeaponAttackUnits.Contains(command.AttackerId))
    {
        return; // Ignore duplicate
    }
    
    _pendingWeaponAttackUnits.Add(command.AttackerId);
    CommandPublisher.PublishCommand(command);
}
```

3. **Clear pending on server response**:
```csharp
case WeaponAttackDeclarationCommand weaponAttackDeclarationCommand:
    OnWeaponsAttack(weaponAttackDeclarationCommand);
    _pendingWeaponAttackUnits.Remove(weaponAttackDeclarationCommand.AttackerId);
    break;
```

**Pros**:
- Prevents duplicate commands at source
- Reduces network traffic
- Immediate user feedback (button disabled)

**Cons**:
- Client state can get out of sync if server rejects command
- Requires timeout mechanism for stuck commands
- More complex state management

### Solution 3: Command Sequence Numbers

**Approach**: Add sequence numbers to commands and track processed sequences.

**Implementation**:

1. **Add sequence number to commands**:
```csharp
public record struct WeaponAttackDeclarationCommand : IClientCommand
{
    public required Guid GameOriginId { get; set; }
    public required Guid AttackerId { get; init; }
    public required List<WeaponTargetData> WeaponTargets { get; init; }
    public required Guid PlayerId { get; init; }
    public DateTime Timestamp { get; set; }
    public long SequenceNumber { get; init; } // NEW
}
```

2. **Track processed sequences on server**:
```csharp
private readonly Dictionary<Guid, long> _lastProcessedSequence = new();

private bool ValidateWeaponAttackCommand(WeaponAttackDeclarationCommand command)
{
    var key = command.AttackerId;
    
    if (_lastProcessedSequence.TryGetValue(key, out var lastSeq))
    {
        if (command.SequenceNumber <= lastSeq)
        {
            return false; // Duplicate or out-of-order
        }
    }
    
    _lastProcessedSequence[key] = command.SequenceNumber;
    return true;
}
```

**Pros**:
- Handles out-of-order delivery
- Detects duplicates reliably
- Works across network failures

**Cons**:
- Requires changes to all command types
- More complex implementation
- Sequence number management overhead

### Solution 4: Hybrid Approach (BEST PRACTICE)

**Combine Solutions 1 and 2 for defense in depth**:

1. **Client-side**: Track pending commands (Solution 2)
   - Prevents most duplicates at source
   - Provides immediate UI feedback
   - Reduces network traffic

2. **Server-side**: Idempotency validation (Solution 1)
   - Final safeguard against race conditions
   - Handles edge cases (network issues, malicious clients)
   - Source of truth

3. **Optional**: Add command acknowledgment
   - Server sends ACK/NACK for each command
   - Client clears pending on ACK
   - Client retries on timeout (with exponential backoff)

## Implementation Recommendations

### Phase 1: Immediate Fix (Server-Side Idempotency)

**Priority**: HIGH  
**Effort**: LOW  
**Risk**: LOW

Implement Solution 1:
1. Add `ValidateWeaponAttackCommand()` to `BaseGame.cs`
2. Make `Unit.DeclareWeaponAttack()` idempotent
3. Add unit tests for duplicate command scenarios

**Files to modify**:
- `src/MakaMek.Core/Models/Game/BaseGame.cs`
- `src/MakaMek.Core/Models/Units/Unit.cs`
- Add tests in `tests/MakaMek.Core.Tests/Models/Game/`

### Phase 2: Enhanced Client Protection (Pending Command Tracking)

**Priority**: MEDIUM  
**Effort**: MEDIUM  
**Risk**: LOW

Implement Solution 2:
1. Add pending command tracking to `ClientGame`
2. Update `WeaponsAttackState` to check pending status
3. Add visual feedback (disable buttons for pending commands)

**Files to modify**:
- `src/MakaMek.Core/Models/Game/ClientGame.cs`
- `src/MakaMek.Presentation/UiStates/WeaponsAttackState.cs`
- `src/MakaMek.Presentation/ViewModels/BattleMapViewModel.cs`

### Phase 3: Apply to All Command Types

**Priority**: MEDIUM  
**Effort**: HIGH  
**Risk**: MEDIUM

Extend the solution to all client commands:
- `DeployUnitCommand`
- `MoveUnitCommand`
- `WeaponConfigurationCommand`
- `TryStandupCommand`
- `ShutdownUnitCommand`
- `StartupUnitCommand`
- `TurnEndedCommand`

### Phase 4: Command Acknowledgment (Future Enhancement)

**Priority**: LOW  
**Effort**: HIGH  
**Risk**: MEDIUM

Implement proper request-response pattern:
1. Add ACK/NACK server commands
2. Add timeout and retry logic
3. Add command correlation IDs

## Testing Strategy

### Unit Tests

1. **Server-side idempotency**:
   - Test duplicate `WeaponAttackDeclarationCommand` is rejected
   - Test `Unit.DeclareWeaponAttack()` is idempotent
   - Test validation rejects commands for units that already acted

2. **Client-side pending tracking**:
   - Test pending command prevents duplicate sends
   - Test pending cleared on server response
   - Test timeout clears stale pending commands

### Integration Tests

1. **Race condition scenarios**:
   - Simulate rapid double-click
   - Simulate network latency
   - Simulate out-of-order delivery

2. **Multi-client scenarios**:
   - Test multiple clients don't interfere
   - Test command ordering across clients

### Manual Testing

1. Test with artificial network delay (100-500ms)
2. Test rapid button clicking
3. Test with multiple local players
4. Test with network disconnection/reconnection

## Conclusion

The current implementation has a clear race condition vulnerability where clients can send multiple commands for the same unit before the server processes the first one. The recommended solution is a **hybrid approach**:

1. **Immediate**: Implement server-side idempotency (Solution 1) as a critical fix
2. **Short-term**: Add client-side pending command tracking (Solution 2) for better UX
3. **Long-term**: Extend to all command types and consider command acknowledgment

This approach provides defense in depth, maintains the existing architecture, and can be implemented incrementally with low risk.

