# Game Shutdown and Lifecycle Management - Architectural Design (CORRECTED)

## Current State Analysis

### Problem Statement
The MakaMek game currently lacks a proper shutdown mechanism. When players navigate back to the menu from an active game:
1. **Server continues running** - `ServerGame.Start()` loop keeps running in background
2. **No client disconnect notification** - Server is unaware when clients leave
3. **No cleanup before new game** - Starting a new game doesn't properly clean up the previous one
4. **Resource leaks** - Subscriptions, network connections, and background tasks are not disposed

### Current Architecture

#### Game Lifecycle Components

**1. GameManager** (`src/MakaMek.Core/Models/Game/GameManager.cs`)
- Singleton service managing server game instance
- Owns `ServerGame`, `INetworkHostService`, and `ICommandLogger`
- `InitializeLobby()` creates server game and starts network host
- `Dispose()` cleans up server game and network host
- **Issue**: Only disposed when application exits, not between games

**2. ServerGame** (`src/MakaMek.Core/Models/Game/ServerGame.cs`)
- Implements `IDisposable`
- `Start()` method runs infinite loop: `while (!_isDisposed && !_isGameOver)`
- `Dispose()` sets flags to exit loop
- Publishes server commands via `CommandPublisher`
- **Issue**: No mechanism to signal game end to clients

**3. ClientGame** (`src/MakaMek.Core/Models/Game/ClientGame.cs`)
- Receives and handles server commands
- Maintains `LocalPlayers` list
- Subscribes to `CommandPublisher` in `BaseGame` constructor
- **Issue**: No cleanup when leaving game, subscriptions remain active

**4. ViewModels**
- `StartNewGameViewModel`: Creates lobby, initializes server
- `BattleMapViewModel`: Displays active game, has `BackToMenuCommand`
- **Issue**: `BackToMenu()` just navigates, doesn't clean up game state

#### Command Flow Architecture

**Server → Client Commands:**
- `ChangePhaseCommand` - Phase transitions
- `ChangeActivePlayerCommand` - Active player changes
- `TurnIncrementedCommand` - Turn increments
- `SetBattleMapCommand` - Map initialization
- `HeatUpdatedCommand`, `MechFallCommand`, etc. - Game state updates

**Client → Server Commands:**
- All implement `IClientCommand` interface
- `JoinGameCommand`, `DeployUnitCommand`, `MoveUnitCommand`, etc.

**Command Publisher/Subscriber Pattern:**
- `CommandPublisher.Subscribe(Action<IGameCommand>)` - Adds subscriber
- `CommandPublisher.PublishCommand(IGameCommand)` - Broadcasts to all
- **Issue**: No `Unsubscribe()` method exists

## Proposed Solution (CORRECTED)

### Key Architectural Principle
**Maintain Client-Server Separation**: Clients communicate with server ONLY through commands, never direct method calls.

### 1. Game Shutdown Commands

Create two new commands for the shutdown flow:

**A. Client Command - PlayerLeftCommand**

**File**: `src/MakaMek.Core/Data/Game/Commands/Client/PlayerLeftCommand.cs`

```csharp
public record struct PlayerLeftCommand : IClientCommand
{
    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }
    public required Guid PlayerId { get; init; }
    
    public string Render(ILocalizationService localizationService, IGame game)
    {
        var player = game.Players.FirstOrDefault(p => p.Id == PlayerId);
        var playerName = player?.Name ?? "Unknown";
        var localizedTemplate = localizationService.GetString("Command_PlayerLeft");
        return string.Format(localizedTemplate, playerName);
    }
}
```

**B. Server Command - GameEndedCommand**

**File**: `src/MakaMek.Core/Data/Game/Commands/Server/GameEndedCommand.cs`

```csharp
public record struct GameEndedCommand : IGameCommand
{
    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }
    public required GameEndReason Reason { get; init; }
    
    public string Render(ILocalizationService localizationService, IGame game)
    {
        var key = $"Command_GameEnded_{Reason}";
        return localizationService.GetString(key);
    }
}

public enum GameEndReason
{
    HostDisconnected,
    GameAborted,
    Victory,
    AllPlayersLeft
}
```

### 2. ServerGame Shutdown Logic

Add shutdown method to `ServerGame` that handles game termination:

**File**: `src/MakaMek.Core/Models/Game/ServerGame.cs`

```csharp
public class ServerGame : BaseGame, IDisposable
{
    // Existing fields...
    
    /// <summary>
    /// Stops the game and notifies all clients
    /// </summary>
    public void StopGame(GameEndReason reason)
    {
        if (_isDisposed || _isGameOver) return;
        
        // 1. Publish GameEndedCommand to all clients
        var endCommand = new GameEndedCommand
        {
            GameOriginId = Id,
            Reason = reason,
            Timestamp = DateTime.UtcNow
        };
        CommandPublisher.PublishCommand(endCommand);
        
        // 2. Mark game as over (will exit Start() loop)
        _isGameOver = true;
    }
    
    public override void HandleCommand(IGameCommand command)
    {
        if (!ShouldHandleCommand(command)) return;
        
        switch (command)
        {
            // Existing cases...
            
            case PlayerLeftCommand playerLeftCommand:
                HandlePlayerLeft(playerLeftCommand);
                break;
        }
        
        // Existing logic...
    }
    
    private void HandlePlayerLeft(PlayerLeftCommand command)
    {
        // Remove player from game
        var player = Players.FirstOrDefault(p => p.Id == command.PlayerId);
        if (player != null)
        {
            Players.Remove(player);
            
            // Check if all players have left
            if (Players.Count == 0)
            {
                StopGame(GameEndReason.AllPlayersLeft);
            }
        }
    }
}
```

### 3. Enhanced ICommandPublisher

Add unsubscribe capability:

**File**: `src/MakaMek.Core/Services/Transport/ICommandPublisher.cs`

```csharp
public interface ICommandPublisher
{
    void PublishCommand(IGameCommand command);
    void Subscribe(Action<IGameCommand> onCommandReceived, ITransportPublisher? transportPublisher = null);
    void Unsubscribe(Action<IGameCommand> onCommandReceived); // NEW
    CommandTransportAdapter Adapter { get; }
}
```

**Implementation in CommandPublisher.cs**:

```csharp
public void Unsubscribe(Action<IGameCommand> onCommandReceived)
{
    _subscribers.Remove(onCommandReceived);
    _subscriberTransports.Remove(onCommandReceived);
}
```

### 4. GameManager Reset Logic

Update `GameManager` to handle cleanup between games (NO direct StopGame call):

**File**: `src/MakaMek.Core/Models/Game/GameManager.cs`

```csharp
public class GameManager : IGameManager
{
    private ServerGame? _serverGame;
    private bool _isGameActive;
    
    public bool IsGameActive => _isGameActive && _serverGame != null;
    
    public async Task ResetForNewGame()
    {
        // Dispose current server game if exists
        if (_serverGame != null)
        {
            _serverGame.Dispose();
            _serverGame = null;
            _isGameActive = false;
        }
        
        // Unsubscribe logging handler
        if (_logHandler != null)
        {
            _commandPublisher.Unsubscribe(_logHandler);
            _loggingSubscribed = false;
        }
        
        // Dispose command logger
        _commandLogger?.Dispose();
        _commandLogger = null;
        
        // Clear transport publishers (but keep network host running)
        var transportAdapter = _commandPublisher.Adapter;
        var networkPublisher = _networkHostService?.Publisher;
        
        transportAdapter.ClearPublishers();
        
        if (networkPublisher != null)
        {
            transportAdapter.AddPublisher(networkPublisher);
        }
    }
    
    public async Task InitializeLobby()
    {
        // Reset before initializing new lobby
        await ResetForNewGame();
        
        // Existing initialization logic...
        var transportAdapter = _commandPublisher.Adapter;
        
        if (CanStartLanServer && !IsLanServerRunning && _networkHostService != null)
        {
            await _networkHostService.Start();
            if (_networkHostService.Publisher != null)
            {
                transportAdapter.AddPublisher(_networkHostService.Publisher);
            }
        }
        
        // Create new server game
        _serverGame = _gameFactory.CreateServerGame(/*...*/);
        _ = Task.Run(() => _serverGame?.Start());
        _isGameActive = true;
        
        // Setup logging...
    }
}
```

### 5. ClientGame Cleanup

Add cleanup method to `ClientGame`:

```csharp
public sealed class ClientGame : BaseGame, IDisposable
{
    private bool _isDisposed;
    
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        
        // Complete and dispose subjects
        _commandSubject.OnCompleted();
        _commandSubject.Dispose();
        
        GC.SuppressFinalize(this);
    }
    
    public override void HandleCommand(IGameCommand command)
    {
        if (!ShouldHandleCommand(command)) return;
        
        switch (command)
        {
            // Existing cases...
            
            case GameEndedCommand gameEndedCommand:
                OnGameEnded(gameEndedCommand);
                break;
        }
        
        // Existing logic...
    }
    
    private void OnGameEnded(GameEndedCommand command)
    {
        // Log and publish to subscribers
        _commandLog.Add(command);
        _commandSubject.OnNext(command);
    }
}
```

### 6. BattleMapViewModel Integration (CORRECTED)

**NO IGameManager dependency** - Use commands only:

```csharp
public class BattleMapViewModel : BaseViewModel, IDisposable
{
    private readonly ICommandPublisher _commandPublisher;
    private bool _isDisposed;
    
    public BattleMapViewModel(
        ICommandPublisher commandPublisher, // NOT IGameManager
        ILocalizationService localizationService,
        IRulesProvider rulesProvider,
        IDispatcherService dispatcherService)
    {
        _commandPublisher = commandPublisher;
        BackToMenuCommand = new AsyncCommand(BackToMenu);
    }
    
    private async Task BackToMenu()
    {
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
        
        // Cleanup local game
        Dispose();
        
        // Navigate to menu
        await NavigationService.NavigateToRootAsync();
    }
    
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        
        _gameSubscription?.Dispose();
        _commandSubscription?.Dispose();
        (_game as IDisposable)?.Dispose();
        
        GC.SuppressFinalize(this);
    }
    
    private void OnCommandReceived(IGameCommand command)
    {
        if (command is GameEndedCommand)
        {
            // Server ended the game - navigate back
            _dispatcherService.RunOnUIThread(async () => await BackToMenu());
        }
    }
}
```

## Implementation Checklist

### Phase 1: Core Infrastructure
- [ ] Create `PlayerLeftCommand` (client command)
- [ ] Create `GameEndedCommand` and `GameEndReason` enum (server command)
- [ ] Add `Unsubscribe()` method to `ICommandPublisher` and `CommandPublisher`
- [ ] Update `GameManager.ResetForNewGame()` to use Unsubscribe

### Phase 2: Server-Side Shutdown
- [ ] Add `StopGame(GameEndReason)` method to `ServerGame`
- [ ] Add `HandlePlayerLeft()` to `ServerGame.HandleCommand()`
- [ ] Add validation for `PlayerLeftCommand` in `BaseGame.ValidateCommand()`

### Phase 3: Client-Side Handling
- [ ] Add `IDisposable` to `ClientGame` with proper cleanup
- [ ] Implement `OnGameEnded()` in `ClientGame`
- [ ] Add `GameEndedCommand` to `BaseGame.ValidateCommand()`

### Phase 4: ViewModel Integration
- [ ] Remove `IGameManager` dependency from `BattleMapViewModel`
- [ ] Implement `IDisposable` in `BattleMapViewModel`
- [ ] Update `BackToMenu()` to send `PlayerLeftCommand`
- [ ] Handle `GameEndedCommand` in `BattleMapViewModel`

### Phase 5: Testing
- [ ] Unit tests for `ServerGame.StopGame()`
- [ ] Unit tests for `ServerGame.HandlePlayerLeft()`
- [ ] Unit tests for `CommandPublisher.Unsubscribe()`
- [ ] Integration tests for shutdown flow
- [ ] Test multiple game start/stop cycles

## Benefits

1. **Proper Client-Server Separation**: Commands-only communication
2. **Clean Shutdown**: Proper cleanup of all resources
3. **Client Notification**: All clients informed when game ends
4. **Resource Management**: No memory leaks or zombie processes
5. **Multiple Games**: Can start new games without restarting app
6. **Testability**: Clear lifecycle makes testing easier

