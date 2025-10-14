# Game Shutdown Edge Cases and Best Practices (CORRECTED)

## Overview

This document covers edge cases, error scenarios, and best practices for the game shutdown implementation with proper client-server separation. It complements the main architectural design and implementation guide.

## Edge Cases

### 1. Network Disconnection During Shutdown

**Scenario**: Server sends `GameEndedCommand` but network fails before clients receive it.

**Problem**: Clients remain in game state, unaware the server has shut down.

**Solution**: Add heartbeat detection in ClientGame

```csharp
// ClientGame.cs - Add heartbeat detection
private Timer? _heartbeatTimer;
private DateTime _lastCommandReceived;

public ClientGame(/* ... */)
{
    // Start heartbeat monitor
    _heartbeatTimer = new Timer(CheckServerHeartbeat, null, 
        TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    _lastCommandReceived = DateTime.UtcNow;
}

private void CheckServerHeartbeat(object? state)
{
    var timeSinceLastCommand = DateTime.UtcNow - _lastCommandReceived;
    
    // If no command received in 30 seconds, assume disconnected
    if (timeSinceLastCommand > TimeSpan.FromSeconds(30))
    {
        // Simulate GameEndedCommand
        var disconnectCommand = new GameEndedCommand
        {
            GameOriginId = Id,
            Reason = GameEndReason.HostDisconnected,
            Timestamp = DateTime.UtcNow
        };
        HandleCommand(disconnectCommand);
    }
}

public override void HandleCommand(IGameCommand command)
{
    _lastCommandReceived = DateTime.UtcNow;
    // ... existing logic
}

public void Dispose()
{
    _heartbeatTimer?.Dispose();
    // ... existing disposal
}
```

### 2. Multiple Simultaneous BackToMenu Calls

**Scenario**: User rapidly clicks "Back to Menu" button multiple times.

**Problem**: Multiple `PlayerLeftCommand` sent, potential race conditions.

**Solution**: Add guard flag in BattleMapViewModel

```csharp
// BattleMapViewModel.cs
private bool _isNavigatingBack;
private readonly SemaphoreSlim _navigationLock = new(1, 1);

private async Task BackToMenu()
{
    // Guard against multiple simultaneous calls
    if (_isNavigatingBack) return;
    
    await _navigationLock.WaitAsync();
    try
    {
        if (_isNavigatingBack) return;
        _isNavigatingBack = true;
        
        // Send PlayerLeftCommand for each local player
        foreach (var player in Game.LocalPlayers)
        {
            var playerLeftCommand = new PlayerLeftCommand
            {
                GameOriginId = Game.Id,
                PlayerId = player.Id,
                Timestamp = DateTime.UtcNow
            };
            _commandPublisher.PublishCommand(playerLeftCommand);
        }
        
        await Task.Delay(100);
        Dispose();
        await NavigationService.NavigateToRootAsync();
    }
    finally
    {
        _navigationLock.Release();
    }
}

public void Dispose()
{
    // ... existing disposal
    _navigationLock?.Dispose();
}
```

### 3. Player Leaves During Critical Phase

**Scenario**: Player leaves during deployment or combat phase.

**Problem**: Game state may be inconsistent (e.g., waiting for player's action).

**Solution**: Handle player removal in ServerGame with phase awareness

```csharp
// ServerGame.cs
private void HandlePlayerLeft(PlayerLeftCommand command)
{
    var player = Players.FirstOrDefault(p => p.Id == command.PlayerId);
    if (player == null) return;
    
    // If it's this player's turn, advance to next player
    if (ActivePlayer?.Id == player.Id)
    {
        AdvanceToNextPlayer();
    }
    
    // Remove all units owned by this player
    var playerUnits = UnitsToPlay.Where(u => u.OwnerId == player.Id).ToList();
    foreach (var unit in playerUnits)
    {
        UnitsToPlay.Remove(unit);
    }
    
    // Remove player
    Players.Remove(player);
    
    // Check if all players have left
    if (Players.Count == 0)
    {
        StopGame(GameEndReason.AllPlayersLeft);
    }
}
```

### 4. Duplicate PlayerLeftCommand

**Scenario**: Client sends `PlayerLeftCommand` multiple times (e.g., network retry).

**Problem**: Server tries to remove already-removed player.

**Solution**: Make HandlePlayerLeft idempotent

```csharp
// ServerGame.cs
private void HandlePlayerLeft(PlayerLeftCommand command)
{
    var player = Players.FirstOrDefault(p => p.Id == command.PlayerId);
    if (player == null)
    {
        // Player already removed - ignore
        return;
    }
    
    // ... rest of logic
}
```

### 5. Server Crash Without Graceful Shutdown

**Scenario**: Server process crashes or is force-killed.

**Problem**: No `GameEndedCommand` sent to clients.

**Solution**: Combine heartbeat detection (Edge Case #1) with connection state monitoring

```csharp
// CommandTransportAdapter.cs - Add connection state events
public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

private void OnPublisherDisconnected(ITransportPublisher publisher)
{
    ConnectionStateChanged?.Invoke(this, 
        new ConnectionStateChangedEventArgs(publisher, false));
}

// ClientGame.cs - Subscribe to connection events
public ClientGame(/* ... */)
{
    CommandPublisher.Adapter.ConnectionStateChanged += OnConnectionStateChanged;
}

private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
{
    if (!e.IsConnected)
    {
        // Server disconnected - trigger cleanup
        var disconnectCommand = new GameEndedCommand
        {
            GameOriginId = Id,
            Reason = GameEndReason.HostDisconnected,
            Timestamp = DateTime.UtcNow
        };
        HandleCommand(disconnectCommand);
    }
}
```

### 6. Starting New Game While Previous Game Shutting Down

**Scenario**: User starts new game before previous game fully shut down.

**Problem**: Two games running simultaneously, resource conflicts.

**Solution**: Add shutdown completion tracking in GameManager

```csharp
// GameManager.cs
private Task? _resetTask;

public async Task ResetForNewGame()
{
    if (_resetTask != null)
    {
        // Already resetting - wait for completion
        await _resetTask;
        return;
    }
    
    _resetTask = ResetForNewGameInternal();
    await _resetTask;
    _resetTask = null;
}

private async Task ResetForNewGameInternal()
{
    // Dispose current server game if exists
    if (_serverGame != null)
    {
        _serverGame.Dispose();
        
        // Wait a bit for disposal to complete
        await Task.Delay(200);
        
        _serverGame = null;
        _isGameActive = false;
    }
    
    // ... rest of reset logic
}

public async Task InitializeLobby()
{
    // Wait for any pending reset
    if (_resetTask != null)
    {
        await _resetTask;
    }
    
    // ... existing initialization logic
}
```

### 7. Remote Client Closes App Without Sending PlayerLeftCommand

**Scenario**: Remote client closes app abruptly without sending disconnect command.

**Problem**: Server doesn't know client left, may wait for their actions.

**Solution**: Track player activity and detect timeouts

```csharp
// ServerGame.cs - Track player activity
private readonly Dictionary<Guid, DateTime> _playerLastActivity = new();
private Timer? _activityMonitor;

public ServerGame(/* ... */)
{
    // Monitor player activity every 10 seconds
    _activityMonitor = new Timer(CheckPlayerActivity, null,
        TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
}

public override void HandleCommand(IGameCommand command)
{
    // Update activity timestamp for player commands
    if (command is IClientCommand clientCommand)
    {
        _playerLastActivity[clientCommand.PlayerId] = DateTime.UtcNow;
    }
    
    // Existing logic
}

private void CheckPlayerActivity(object? state)
{
    var inactivePlayers = _playerLastActivity
        .Where(kvp => DateTime.UtcNow - kvp.Value > TimeSpan.FromMinutes(2))
        .Select(kvp => kvp.Key)
        .ToList();
    
    foreach (var playerId in inactivePlayers)
    {
        // Simulate PlayerLeftCommand for inactive player
        var playerLeftCommand = new PlayerLeftCommand
        {
            GameOriginId = Id,
            PlayerId = playerId,
            Timestamp = DateTime.UtcNow
        };
        HandleCommand(playerLeftCommand);
        
        _playerLastActivity.Remove(playerId);
    }
}

public void Dispose()
{
    _activityMonitor?.Dispose();
    // Existing disposal
}
```

### 8. Memory Leaks from Event Subscriptions

**Scenario**: Event handlers not unsubscribed, keeping objects alive.

**Problem**: Memory leaks over multiple game cycles.

**Solution**: Comprehensive subscription tracking

```csharp
// BattleMapViewModel.cs - Track all subscriptions
private readonly List<IDisposable> _subscriptions = new();

public BattleMapViewModel(/* ... */)
{
    // Track all subscriptions
    _subscriptions.Add(Game.PhaseChanges.Subscribe(OnPhaseChanged));
    _subscriptions.Add(Game.ActivePlayerChanges.Subscribe(OnActivePlayerChanged));
    _subscriptions.Add(Game.Commands.Subscribe(OnCommandReceived));
}

public void Dispose()
{
    if (_isDisposed) return;
    _isDisposed = true;
    
    // Dispose all subscriptions
    foreach (var subscription in _subscriptions)
    {
        subscription?.Dispose();
    }
    _subscriptions.Clear();
    
    // Existing disposal
}
```

## Best Practices

### 1. Always Use Command Pattern for Client-Server Communication

```csharp
// GOOD - Using commands
var playerLeftCommand = new PlayerLeftCommand
{
    GameOriginId = Game.Id,
    PlayerId = player.Id,
    Timestamp = DateTime.UtcNow
};
_commandPublisher.PublishCommand(playerLeftCommand);

// BAD - Direct method call (violates client-server separation)
_gameManager.StopGame(GameEndReason.GameAborted);
```

### 2. Make Command Handlers Idempotent

```csharp
// Command handlers should handle duplicate commands gracefully
private void HandlePlayerLeft(PlayerLeftCommand command)
{
    var player = Players.FirstOrDefault(p => p.Id == command.PlayerId);
    if (player == null) return; // Already removed - safe to ignore
    
    // ... rest of logic
}
```

### 3. Always Implement IDisposable Properly

```csharp
public void Dispose()
{
    if (_isDisposed) return; // Guard against multiple calls
    _isDisposed = true;
    
    // Dispose managed resources
    _timer?.Dispose();
    _subscription?.Dispose();
    
    GC.SuppressFinalize(this);
}
```

### 4. Use Cancellation Tokens for Long Operations

```csharp
public async Task BackToMenu(CancellationToken cancellationToken = default)
{
    // Send commands
    foreach (var player in Game.LocalPlayers)
    {
        _commandPublisher.PublishCommand(new PlayerLeftCommand { /*...*/ });
    }
    
    // Respect cancellation
    await Task.Delay(100, cancellationToken);
    
    Dispose();
    await NavigationService.NavigateToRootAsync();
}
```

### 5. Log All Lifecycle Events

```csharp
// ServerGame.cs
public void StopGame(GameEndReason reason)
{
    _logger?.LogInformation($"Stopping game {Id} - Reason: {reason}");
    
    // Shutdown logic
    
    _logger?.LogInformation("Game stopped successfully");
}
```

## Testing Recommendations

### Unit Test Edge Cases

```csharp
[Fact]
public void HandlePlayerLeft_CalledTwice_OnlyRemovesOnce()
{
    // Arrange
    var player = CreatePlayer();
    var sut = CreateServerGameWithPlayer(player);
    var command = new PlayerLeftCommand { PlayerId = player.Id };
    
    // Act
    sut.HandleCommand(command);
    sut.HandleCommand(command); // Second call
    
    // Assert - should not throw
    sut.Players.ShouldNotContain(player);
}

[Fact]
public async Task BackToMenu_CalledMultipleTimes_OnlySendsCommandsOnce()
{
    // Test guard against multiple calls
}

[Fact]
public void Dispose_CalledMultipleTimes_DoesNotThrow()
{
    // Test idempotent disposal
}
```

### Integration Test Scenarios

```csharp
[Fact]
public async Task PlayerLeaves_ServerRemovesPlayer_OtherClientsNotified()
{
    // Test full flow of player leaving
}

[Fact]
public async Task AllPlayersLeave_ServerStopsGame_SendsGameEndedCommand()
{
    // Test game shutdown when all players leave
}
```

## Summary

Proper edge case handling with client-server separation ensures:
1. **Architectural Integrity**: Commands-only communication maintained
2. **Robustness**: System handles failures gracefully
3. **Resource Management**: No leaks or zombie processes
4. **User Experience**: Smooth transitions, no hangs
5. **Maintainability**: Clear patterns for future development

