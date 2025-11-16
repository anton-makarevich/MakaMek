# Bot Player System - Product Requirements Document
**Date:** 2025-10-16  
**Updated:** 2025-11-16
**Status:** Ready for Implementation

## Executive Summary

This document outlines the design for implementing AI bot players in the MakaMek BattleTech game. The bot system will integrate seamlessly with the existing client-server architecture, allowing bots to participate in games alongside human players with complete network transparency.

## Architecture Overview

### Current Client-Server Architecture

The MakaMek game uses a command-based client-server architecture:

- **ServerGame**: Authoritative game state, validates commands, manages phases, publishes server commands
- **ClientGame**: Client-side game state, receives server commands, publishes client commands
- **Transport Layer**: RxTransportPublisher (local) and SignalRTransportPublisher (network) enable communication
- **Command Pattern**: All game actions are represented as commands (IClientCommand, IServerCommand)
- **Game Phases**: Start → Deployment → Initiative → Movement → WeaponsAttack → WeaponAttackResolution → Heat → End

### Bot Integration Strategy

**Core Principle:** `ClientGame` is agnostic about how players make decisions. It only cares about receiving and sending commands.

Bots will integrate by:

- **Reusing ClientGame**: Bots use the same `ClientGame` instance as human players
- **RX Transport**: Bots communicate via `RxTransportPublisher`, identical to local human players
- **Network Transparency**: Remote players cannot distinguish bots from human players
- **Decision Engine**: Bot logic observes game state and publishes appropriate commands
- **BotManager Responsibility**: The `BotManager` tracks which players are bots and manages their lifecycle - `ClientGame` remains unaware

## Component Design

### Component Interfaces

#### IPlayer Enhancement

```csharp
/// <summary>
/// Player metadata to indicate player type for UI purposes only
/// </summary>
public enum PlayerControlType
{
    Local,      // Human player on this client
    Bot,        // AI-controlled player on this client
    Remote      // Human player on another client
}

public interface IPlayer
{
    Guid Id { get; }
    string Name { get; }
    
    /// <summary>
    /// Indicates how this player is controlled (for UI display only)
    /// This is metadata, not behavior - ClientGame doesn't use this
    /// </summary>
    PlayerControlType ControlType { get; }
    
    // ... existing properties
}
```

#### IBot Interface

```csharp
/// <summary>
/// Interface for bot players that can make automated decisions
/// </summary>
public interface IBot : IDisposable
{
    /// <summary>
    /// The player instance associated with this bot
    /// </summary>
    IPlayer Player { get; }
    
    /// <summary>
    /// The client game instance used by this bot
    /// </summary>
    ClientGame ClientGame { get; }
    
    /// <summary>
    /// Bot difficulty/strategy level
    /// </summary>
    BotDifficulty Difficulty { get; }
}
```

#### Bot Implementation

```csharp
public class Bot : IBot
{
    private readonly IBotDecisionEngine _decisionEngine;
    private IDisposable? _commandSubscription;
    
    // Depends on current phase, stateless, same concept as UIState for human players
    private IBotDecisionEngine _currentDecisionEngine;
    
    public IPlayer Player { get; }
    public ClientGame ClientGame { get; }
    public BotDifficulty Difficulty { get; }
    
    public Bot(
        IPlayer player,
        ClientGame clientGame,
        BotDifficulty difficulty)
    {
        Player = player;
        ClientGame = clientGame;
        Difficulty = difficulty;
        
        _commandSubscription = ClientGame.Commands.Subscribe(OnCommandReceived);
    }
    
    private void OnCommandReceived(IGameCommand command)
    {
        switch (command)
        {
            case ChangeActivePlayerCommand activePlayerCmd:
                if (activePlayerCmd.PlayerId == Player.Id)
                    _ = _currentDecisionEngine.MakeDecision();
                break;
                
            case ChangePhaseCommand phaseCmd:
                _currentDecisionEngine = CreateDecisionEngine(phaseCmd.Phase);
                break;
                
            case GameEndedCommand:
                Dispose();
                break;
                
            // Ignore other commands
        }
    }
    
    private IBotDecisionEngine CreateDecisionEngine(GamePhase phase)
    {
        // Factory logic to create appropriate decision engine based on phase
        return phase switch
        {
            GamePhase.Deployment => new DeploymentEngine(ClientGame, Player),
            GamePhase.Movement => new MovementEngine(ClientGame, Player),
            GamePhase.WeaponsAttack => new WeaponsEngine(ClientGame, Player),
            GamePhase.End => new EndPhaseEngine(ClientGame, Player),
            _ => new NoOpEngine()
        };
    }
    
    private int GetThinkingDelay()
    {
        // optionally introduce delay's to Bot's decision making to make it feel more natural
    }
    
    public void Dispose()
    {
        _commandSubscription?.Dispose();
    }
}
```

#### BotDifficulty Enum

```csharp
namespace Sanet.MakaMek.Core.Models.Game.Players.Bots;

public enum BotDifficulty
{
    Easy,
    Medium,
    Hard
}
```

### Decision Engine

#### IBotDecisionEngine Interface

```csharp
/// <summary>
/// Interface for bot decision-making logic
/// </summary>
public interface IBotDecisionEngine
{
    /// <summary>
    /// Selects the best action for the current game state
    /// </summary>
    Task MakeDecision(BotStrategy? state = null);
}
```

#### Phase-Specific Decision Engines

```csharp
// Base implementation
public class DeploymentEngine : IBotDecisionEngine
{
    private readonly ClientGame _game;
    private readonly IPlayer _player;
    
    public DeploymentEngine(ClientGame game, IPlayer player)
    {
        _game = game;
        _player = player;
    }
    
    public async Task MakeDecision(BotStrategy? state = null)
    {
        // ... logic to select deployment action
        // Can use Builders to create commands and publish them via _game
    }
}
```

Other engines follow the same pattern.

### Bot Management

#### IBotManager Interface

```csharp
/// <summary>
/// Manages/hosts bot players for a game
/// Responsible for bot lifecycle and tracking which players are bots
/// </summary>
public interface IBotManager : IDisposable
{
    /// <summary>
    /// Initializes the bot manager with the client game, resets any existing state
    /// </summary>
    void Initialize(ClientGame clientGame);
    
    /// <summary>
    /// Creates and adds a bot player to the game
    /// </summary>
    void AddBot(IPlayer player, BotDifficulty difficulty);
    
    /// <summary>
    /// Removes a bot player from the game
    /// </summary>
    void RemoveBot(Guid botPlayerId);
    
    /// <summary>
    /// Gets all active bots
    /// </summary>
    IReadOnlyList<IBot> Bots { get; }
    
    /// <summary>
    /// Checks if a given player is controlled by a bot
    /// </summary>
    bool IsBot(Guid playerId);
}
```

#### BotManager Implementation

```csharp
public class BotManager : IBotManager
{
    private readonly List<IBot> _bots = [];
    private ClientGame? _clientGame;
    
    public void Initialize(ClientGame clientGame)
    {
        _clientGame = clientGame;
        
        // Clean up any existing bots
        foreach (var bot in _bots.Values)
        {
            bot.Dispose();
        }
        _bots.Clear();
    }
    
    public IReadOnlyList<IBot> Bots => _bots.Values.ToList();
    
    public void AddBot(IPlayer player, BotDifficulty difficulty = BotDifficulty.Easy)
    {
        if (_clientGame == null)
            throw new InvalidOperationException("BotManager must be initialized before adding bots");
        
        // Ensure player has correct control type
        if (player.ControlType != PlayerControlType.Bot)
            throw new ArgumentException("Player must have ControlType.Bot", nameof(player));
        
        // Join the game like any local player - ClientGame doesn't know it's a bot
        _clientGame.JoinGameWithUnits(player, units, pilotAssignments);
        
        // BotManager tracks which players are bots
        var bot = new Bot(player, _clientGame, difficulty);
        _bots.Add(player.Id, bot);
    }
    
    public void RemoveBot(Guid botPlayerId)
    {
        if (_bots.TryGetValue(botPlayerId, out var bot))
        {
            bot.Dispose();
            _bots.Remove(botPlayerId);
            
            // Remove from game
            // _clientGame.RemovePlayer(botPlayerId);
        }
    }
    
    public bool IsBot(Guid playerId)
    {
        return _bots.ContainsKey(playerId);
    }
    
    public void Dispose()
    {
        foreach (var bot in _bots.Values)
        {
            bot.Dispose();
        }
        _bots.Clear();
    }
}
```

---

## ClientGame: Agnostic Design

**Important** `ClientGame` does NOT have a `BotPlayers` list or any bot-specific logic.

```csharp
public class ClientGame
{
    /// <summary>
    /// All players local to this client (human or bot)
    /// ClientGame doesn't distinguish between them
    /// </summary>
    public IReadOnlyList<IPlayer> LocalPlayers { get; }
    
    // No BotPlayers list!
    // No IsBot flags or checks!
    
    public void JoinGameWithUnits(IPlayer player, List<Unit> units, Dictionary<Guid, Pilot> pilotAssignments)
    {
        // Same logic for all local players
        // Player.ControlType is just metadata for UI - not used here
        
        LocalPlayers.Add(player);
        
        // ... existing join logic
    }
}
```

---

## Integration Workflow

### Pre-Game Setup (in lobby)

1. `BotManager` is initialized with `ClientGame` instance as soon as it's created
2. User selects "Add Bot" option
3. UI specifies bot difficulty and units
4. Create `Player` instance with:
   - Unique ID and name
   - `ControlType = PlayerControlType.Bot`
5. `BotManager.AddBot()` is called

### Bot Creation

1. `BotManager` adds the player to `ClientGame` via `JoinGameWithUnits()` (no special flags)
2. `ClientGame` treats it as a local player (adds to `LocalPlayers`)
3. `BotManager` creates `Bot` instance and tracks it internally
4. `Bot` subscribes to `ClientGame.Commands`

### Server Registration

1. Bot's `JoinGameCommand` is processed by `ServerGame`
2. Server creates player and units in authoritative state (no difference from human players)
3. Other clients receive `JoinGameCommand` and see bot as regular player

### Game Execution

1. **Phase Activation:**
   - `ServerGame` transitions to phase (e.g., Deployment)
   - Bot's `ClientGame` receives `ChangePhaseCommand`
   - `Bot` updates its decision engine

2. **Turn Activation:**
   - `ServerGame` sets active player via `ChangeActivePlayerCommand`
   - Bot's `ClientGame` receives command
   - `Bot` observes command and acts if it matches its player ID

3. **Decision Process:**
   - Decision engine analyzes game state
   - Publishes command via `ClientGame` (same as human player)
   - Command flows through transport to `ServerGame`

4. **Server Processing:**
   - Server validates and processes (doesn't know it's from a bot)
   - Server broadcasts result to all clients

### Game End

1. Each bot unsubscribes from events
2. `BotManager` disposes all bots
3. Bots are cleared on next game start

## Communication Flow

### Bot Command Flow

```
Bot Decision Engine
    ↓ (selects action)
DecisionEngine.MakeDecision()
    ↓ (publishes command)
ClientGame.SendPlayerAction()
    ↓
CommandPublisher.PublishCommand()
    ↓
RxTransportPublisher (local transport)
    ↓
ServerGame.HandleCommand()
    ↓ (validates & processes - doesn't know it's a bot)
ServerGame publishes server command
    ↓
RxTransportPublisher broadcasts
    ↓
All ClientGame instances (human + bot + remote)
    ↓
Bot's ClientGame observes via Commands.Subscribe()
```

### Transport Architecture

```
┌─────────────────────────────────────────────────────────┐
│                     ServerGame                          │
│         (Authoritative State, Phase Management)         │
└────────────────┬────────────────────────────────────────┘
                 │
                 │ CommandPublisher
                 │
        ┌────────┴────────┐
        │                 │
        ▼                 ▼
RxTransportPublisher  SignalRTransportPublisher
    (Local)              (Network)
        │                 │
        │          ┌──────┴──────┐
        │          │             │
        ▼          ▼             ▼
   ClientGame  ClientGame   ClientGame
   (Local)     (Remote 1)   (Remote 2)
        │          │             │
    ┌───┴───┐  ┌───┴───┐    ┌───┴───┐
    │       │  │       │    │       │
    ▼       ▼  ▼       ▼    ▼       ▼
   UI   BotManager  UI  BotManager  UI  BotManager
         │
         └─ Manages Bot lifecycle
            Tracks which players are bots
            ClientGame doesn't know
```

## Decision Engine Details

Similar to `UIStates` for human players, bots need phase-specific logic. Some logic can be extracted and shared between specific UIStates and DecisionEngines when appropriate.

### Deployment Engine

```csharp
public class DeploymentEngine : IBotDecisionEngine
{
    private readonly ClientGame _game;
    private readonly IPlayer _player;
    
    public DeploymentEngine(ClientGame game, IPlayer player)
    {
        _game = game;
        _player = player;
    }
    
    public async Task MakeDecision()
    {
        try
        {
            // Find undeployed units
            var undeployedUnits = _player.Units.Where(u => !u.IsDeployed).ToList();
            if (!undeployedUnits.Any()) 
                throw new Exception("No undeployed units");
            
            // Select a unit (random for basic bot, strategic for advanced)
            var unit = SelectUnitToDeploy(undeployedUnits);
            
            // Find valid deployment hexes
            var validHexes = GetValidDeploymentHexes(_game.BattleMap);
            
            // Select deployment position
            var hex = SelectDeploymentHex(validHexes, unit);
            
            // Select facing direction
            var direction = SelectDeploymentFacing(hex, _game);
            
            // Publish deploy command
            var command = new DeployUnitCommand
            {
                GameOriginId = _game.Id,
                PlayerId = _player.Id,
                UnitId = unit.Id,
                Position = hex,
                Direction = (int)direction
            };
            
            _game.DeployUnit(command);
        }
        catch
        {
            // Always skip turn if no action taken
            await SkipTurn();
        }
    }
}
```

### Movement Engine

```csharp
public class MovementEngine : IBotDecisionEngine
{
    private readonly ClientGame _game;
    private readonly IPlayer _player;
    
    public MovementEngine(ClientGame game, IPlayer player)
    {
        _game = game;
        _player = player;
    }
    
    public async Task MakeDecision()
    {
        try
        {
            // Find units that haven't moved this phase
            var unmoved = _player.AliveUnits
                .Where(u => u.IsDeployed && u.MovementTypeUsed == null)
                .ToList();
                
            if (!unmoved.Any()) 
                throw new Exception("No unmoved units");
            
            // Select unit to move
            var unit = SelectUnitToMove(unmoved);
            
            // Determine movement type (walk, run, jump, stand still)
            var movementType = SelectMovementType(unit, _game);
            
            // Calculate movement path
            var path = CalculateMovementPath(unit, movementType, _game);
            
            _game.MoveUnit(new MoveUnitCommand
            {
                GameOriginId = _game.Id,
                PlayerId = _player.Id,
                UnitId = unit.Id,
                MovementType = movementType,
                MovementPath = path
            });
        }
        catch
        {
            // Always skip turn if no action taken
            await SkipTurn();
        }
    }
}
```

### Weapons Attack Engine

```csharp
public class WeaponsEngine : IBotDecisionEngine
{
    private readonly ClientGame _game;
    private readonly IPlayer _player;
    
    public WeaponsEngine(ClientGame game, IPlayer player)
    {
        _game = game;
        _player = player;
    }
    
    public async Task MakeDecision()
    {
        try
        {
            // Find units that haven't declared attacks
            var unattacked = _player.AliveUnits
                .Where(u => !u.HasDeclaredWeaponAttack && u.CanFireWeapons)
                .ToList();
                
            if (!unattacked.Any()) 
                throw new Exception("No units to attack");
            
            // Select attacker
            var attacker = SelectAttacker(unattacked);
            
            // Find potential targets
            var targets = FindPotentialTargets(attacker, _game);
            if (!targets.Any())
            {
                await SkipTurn();
                return;
            }
            
            // Select target
            var target = SelectTarget(targets, attacker, _game);
            
            // Select weapons to fire
            var weapons = SelectWeapons(attacker, target, _game);
            
            // Publish attack declaration command
            var command = new WeaponAttackDeclarationCommand
            {
                GameOriginId = _game.Id,
                PlayerId = _player.Id,
                AttackerId = attacker.Id,
                WeaponTargets = weapons.Select(w => new WeaponTargetData
                {
                    Weapon = w.ToData(),
                    TargetId = target.Id,
                    IsPrimaryTarget = true
                }).ToList()
            };
            
            _game.DeclareWeaponAttack(command);
        }
        catch
        {
            // Always skip turn if no action taken
            await SkipTurn();
        }
    }
}
```

### End Phase Engine

```csharp
public class EndPhaseEngine : IBotDecisionEngine
{
    private readonly ClientGame _game;
    private readonly IPlayer _player;
    
    public EndPhaseEngine(ClientGame game, IPlayer player)
    {
        _game = game;
        _player = player;
    }
    
    public async Task MakeDecision()
    {
        try
        {
            // Check for shutdown units that should attempt restart
            var shutdownUnits = _player.AliveUnits.Where(u => u.IsShutdown).ToList();
            foreach (var unit in shutdownUnits)
            {
                if (ShouldAttemptRestart(unit, _game))
                {
                    StartupUnit(unit.Id);
                }
            }
            
            // Check for overheated units that should shutdown
            var overheatedUnits = _player.AliveUnits
                .Where(u => u.IsActive && ShouldShutdown(u))
                .ToList();
                
            if (overheatedUnits.Any())
            {
                var unit = overheatedUnits.First();
                ShutdownUnit(unit.Id);
            }
        }
        finally
        {
            // Always end turn
            await EndTurn();
        }
    }
}
```

---

## Project Structure

Bot Framework should be developed as a **separate project/dll** with only dependency on `MakaMek.Core`. This enforces correct DI direction and prevents architectural violations.

```
MakaMek.Core/
    Models/
        Game/
            Players/
                IPlayer.cs (enhanced with ControlType)
                PlayerControlType.cs
                
MakaMek.Bots/  (NEW PROJECT)
    IBot.cs
    Bot.cs
    IBotManager.cs
    BotManager.cs
    BotDifficulty.cs
    DecisionEngines/
        IBotDecisionEngine.cs
        DeploymentEngine.cs
        MovementEngine.cs
        WeaponsEngine.cs
        EndPhaseEngine.cs
```

---

## UI Integration

UI should allow adding bots in the lobby:

1. **Reuse Current AddPlayer Logic**: Extend existing UI to create players with `ControlType.Bot`
2. **Add Via BotManager**: Call `BotManager.AddBot()`, NOT `ClientGame` directly
3. **Display Bot Indicators**: Check `player.ControlType` to show bot badges in UI
4. **Remove Bot Functionality**: Call `BotManager.RemoveBot()`

### Example ViewModel Integration

```csharp
public class StartNewGameViewModel
{
    private readonly IBotManager _botManager;
    private readonly ClientGame _clientGame;
    
    public void AddBotPlayer(string name, BotDifficulty difficulty, List<Unit> units)
    {
        var botPlayer = new Player
        {
            Id = Guid.NewGuid(),
            Name = name,
            ControlType = PlayerControlType.Bot  // This is the key difference
        };
        
        // BotManager handles the rest
        _botManager.AddBot(botPlayer, difficulty);
    }
}
```

---

## Dependency Injection

Register bot-related services:

```csharp
// In DI configuration
services.AddSingleton<IBotManager, BotManager>();

// Decision engines can be registered as transient
services.AddTransient<DeploymentEngine>();
services.AddTransient<MovementEngine>();
services.AddTransient<WeaponsEngine>();
services.AddTransient<EndPhaseEngine>();
```

---

## Additional Considerations

### 1. Standup Attempts
Bots need to decide when to attempt standing up.

**Solution:** Include in `MovementEngine` logic - check if unit is prone and decide whether to standup.

### 2. Heat Management
Bots need to make shutdown/startup decisions.

**Solution:** Implemented in `EndPhaseEngine` with heat threshold logic.

### 3. Torso Twisting
Bots need to decide when to rotate torso.

**Solution:** Include in `WeaponsEngine` - evaluate if torso rotation improves firing arcs.

### 4. Thinking Delays
Bots should not respond instantly (feels unnatural).

**Solution:** Implemented via `GetThinkingDelay()` based on difficulty level.

### 5. Unit State Tracking
Bots need to track which units have acted.

**Solution:** Use existing `Unit` properties (`MovementTypeUsed`, `HasDeclaredWeaponAttack`, `IsDeployed`, etc.).

### 6. Partial Information
Bots should only know what their player knows (no cheating).

**Solution:** Bot uses `ClientGame` which only has information broadcast by server.

### 7. Bot Behavior Testing
Difficult to test decision-making logic in isolation.

**Solution:** Mock `ClientGame` and verify correct commands are published for given game states.

---

## Bot Difficulty Levels

The design supports multiple difficulty implementations:

### Easy
- Random valid actions
- Basic target selection (closest enemy)
- No heat management
- Simple movement (random valid hex)

### Medium
- Considers range and line of sight
- Heat-aware weapon selection
- Basic damage potential calculation
- Cover-seeking movement

### Hard
- Advanced tactics (flanking, focus fire)
- Optimal positioning and range management
- Heat management and alpha strikes
- Target prioritization (damaged units, high-value targets)
- Predictive movement (anticipate enemy positions)

---

## Bot Personalities (Future Enhancement)

Different bot personalities can be implemented via strategy pattern:

### Aggressive
- Prioritizes offense over defense
- Closes distance quickly
- Uses run/jump frequently
- Accepts heat buildup for damage

### Defensive
- Maintains optimal range
- Uses cover and terrain
- Conservative heat management
- Focuses on survival

### Balanced
- Mix of offense and defense
- Adapts to situation
- Moderate risk-taking

---

## Future Enhancements

- **Machine Learning**: Train bots on game outcomes using reinforcement learning
- **Adaptive Difficulty**: Adjust bot difficulty based on player win/loss ratio
- **Strategy Patterns**: Recognize and counter player tactics
- **Meta-Learning**: Bots learn from multiple games to improve decision-making

---

## Multiplayer Support

The architecture supports various configurations:

- **Mixed Games**: Human + Bot + Remote players in same game
- **Bot vs Bot**: Fully automated games for testing and simulation
- **Bot Replacement**: Replace disconnected human players with bots mid-game
- **Team Play**: Bots coordinate with human teammates
- **Training Mode**: Bots provide practice opponents for new players

---

## Implementation Plan

### Phase 1: Core Infrastructure
- Implement `PlayerControlType` enum
- Update `IPlayer` interface with `ControlType` property
- Implement `IBot`, `Bot` classes
- Implement `IBotManager`, `BotManager` classes
- Remove any bot-specific logic from `ClientGame` (keep it agnostic)
- Add bot-related dependency injection configuration
- Unit tests for core bot infrastructure

### Phase 2: Basic AI
- Implement `DeploymentEngine` with random deployment logic
- Implement `MovementEngine` with random movement logic
- Implement `WeaponsEngine` with random target selection
- Implement `EndPhaseEngine` with basic end turn logic

### Phase 3: UI Integration
- Add "Add Bot" button to `StartNewGameViewModel`
- Display bots in player list with bot indicator (check `ControlType`)
- Remove bot functionality

### Phase 4: Enhanced AI
- Improve deployment logic (strategic positioning, formation)
- Improve movement logic (pathfinding, cover-seeking, range optimization)
- Improve weapons logic (target prioritization, heat management, weapon selection)
- Implement thinking delays
- Performance optimization

### Phase 5: Testing & Polish
- Unit tests for each decision engine
- Integration tests for bot vs bot games
- Performance testing
- Bug fixes and refinements

---

## Success Criteria

- **Functional**: Bots can complete full games from deployment to victory/defeat without errors
- **Network Transparent**: Remote players cannot distinguish bots from human players via network traffic
- **Performance**: Bots do not cause noticeable lag or performance degradation
- **Usability**: Users can easily add/remove bots from lobby with intuitive UI
- **Extensibility**: New bot strategies and difficulty levels can be added without major refactoring
- **Architectural Integrity**: `ClientGame` remains agnostic about player control mechanisms
- **Reliability**: Bots handle edge cases gracefully (invalid commands, state changes, etc.)
- **Testability**: Bot behavior is testable and reproducible

---

## Summary

The bot player system integrates seamlessly with MakaMek's existing client-server architecture by:

1. **Keeping ClientGame Agnostic**: `ClientGame` has no knowledge of bots - it only tracks `LocalPlayers`
2. **BotManager Owns Bot Concerns**: `BotManager` tracks which players are bots and manages their lifecycle
3. **Metadata for UI**: `PlayerControlType` provides metadata for UI display without affecting game logic
4. **Reusing RX Transport**: Bots communicate via `RxTransportPublisher` like local human players
5. **Phase-Specific Decision Engines**: Similar to UI states, bots use specialized decision engines
6. **Command Pattern**: Bots publish commands through `ClientGame`, validated by `ServerGame`
7. **Extensible Design**: Supports multiple difficulty levels, strategies, and future AI enhancements

### Key Architectural Benefits

- **Separation of Concerns**: Bot logic is separate from UI, server, and client game layers
- **Testability**: Each component can be tested in isolation with mocks
- **Maintainability**: Modular design allows easy updates and extensions
- **Consistency**: Bots use the same mechanisms as human players
- **Network Transparency**: Remote players see bots as regular players
- **Future-Proof**: Easy to add new player control types (spectators, replay systems, AI assistants) without modifying `ClientGame`

The design is ready for implementation with clear component interfaces, well-defined integration points, a phased implementation plan, identified risks and mitigations, and comprehensive success criteria.