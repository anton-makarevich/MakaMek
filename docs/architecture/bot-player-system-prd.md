# Bot Player System - Product Requirements Document

## Executive Summary

This document outlines the design for implementing AI bot players in the MakaMek BattleTech game. The bot system will integrate seamlessly with the existing client-server architecture, allowing bots to participate in games alongside human players with complete network transparency.

## 1. Architecture Overview

### 1.1 Current Client-Server Architecture

The MakaMek game uses a command-based client-server architecture:

- **ServerGame**: Authoritative game state, validates commands, manages phases, publishes server commands
- **ClientGame**: Client-side game state, receives server commands, publishes client commands
- **Transport Layer**: RxTransportPublisher (local) and SignalRTransportPublisher (network) enable communication
- **Command Pattern**: All game actions are represented as commands (IClientCommand, IServerCommand)
- **Game Phases**: Start → Deployment → Initiative → Movement → WeaponsAttack → WeaponAttackResolution → Heat → End

### 1.2 Bot Integration Strategy

Bots will integrate by:

1. **Reusing ClientGame**: Each bot uses a `ClientGame` instance to interact with the server
2. **Server-Side Hosting**: Bot `ClientGame` instances are created and managed on the server side
3. **RX Transport**: Bots communicate via `RxTransportPublisher`, identical to local human players
4. **Network Transparency**: Remote players cannot distinguish bots from human players
5. **Decision Engine**: Bot logic observes game state and publishes appropriate commands

## 2. Component Design

### 2.1 Core Bot Components

#### 2.1.1 IBotPlayer Interface

```csharp
namespace Sanet.MakaMek.Core.Models.Game.Players.Bots;

/// <summary>
/// Interface for bot players that can make automated decisions
/// </summary>
public interface IBotPlayer : IDisposable
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
    
    /// <summary>
    /// Starts the bot's decision-making loop
    /// </summary>
    Task Start(); // do we need this? bot should be active all the time
    
    /// <summary>
    /// Stops the bot's decision-making loop
    /// </summary>
    void Stop();  // do we need this? bot should be active all the time
    
    /// <summary>
    /// Whether the bot is currently active
    /// </summary>
    bool IsActive { get; } // do we need this? bot should be active all the time
}
```

#### 2.1.2 BotPlayer Implementation

```csharp
namespace Sanet.MakaMek.Core.Models.Game.Players.Bots;

public class BotPlayer : IBotPlayer
{
    private readonly ICommandPublisher _commandPublisher; // not needed (client game is responsible for that)
    private readonly IBotDecisionEngine _decisionEngine;
    private IDisposable? _commandSubscription;
    private bool _isActive;
    
    public IPlayer Player { get; }
    public ClientGame ClientGame { get; }
    public BotDifficulty Difficulty { get; }
    public bool IsActive => _isActive;
    
    public BotPlayer(
        IPlayer player,
        ClientGame clientGame,
        IBotDecisionEngine decisionEngine, //should it be injected?
        BotDifficulty difficulty)
    {
        Player = player;
        ClientGame = clientGame;
        _decisionEngine = decisionEngine;
        Difficulty = difficulty;
    }
    
    public Task Start()
    {
        _isActive = true;
        
        // Subscribe to game commands to observe state changes
        _commandSubscription = ClientGame.Commands.Subscribe(OnCommandReceived);
        
        // Subscribe to active player changes
        ClientGame.ActivePlayerChanges.Subscribe(OnActivePlayerChanged);
        
        return Task.CompletedTask;
    }
    
    private void OnCommandReceived(IGameCommand command)
    {
        // Bot observes all commands to maintain awareness of game state
        // Decision engine uses this to update internal state
    }
    
    private async void OnActivePlayerChanged(IPlayer? activePlayer)
    {
        if (!_isActive) return;
        if (activePlayer?.Id != Player.Id) return;
        if (!ClientGame.CanActivePlayerAct) return;
        
        // This bot is now active - make a decision
        await MakeDecision();
    }
    
    private async Task MakeDecision()
    {
        // Add delay to simulate thinking time and avoid instant responses
        await Task.Delay(GetThinkingDelay());
        
        // Get available actions from decision engine
        var action = await _decisionEngine.SelectAction(ClientGame, Player);
        
        if (action != null)
        {
            // Execute the selected action
            action.Execute(ClientGame);
        }
    }
    
    private int GetThinkingDelay()
    {
        // Vary delay based on difficulty
        return Difficulty switch
        {
            BotDifficulty.Easy => Random.Shared.Next(500, 1500),
            BotDifficulty.Medium => Random.Shared.Next(300, 1000),
            BotDifficulty.Hard => Random.Shared.Next(100, 500),
            _ => 500
        };
    }
    
    public void Stop()
    {
        _isActive = false;
        _commandSubscription?.Dispose();
    }
    
    public void Dispose()
    {
        Stop();
        ClientGame.Dispose();
    }
}
```

#### 2.1.3 BotDifficulty Enum

```csharp
namespace Sanet.MakaMek.Core.Models.Game.Players.Bots;

public enum BotDifficulty
{
    Easy,
    Medium,
    Hard
}
```

### 2.2 Decision-Making System

#### 2.2.1 IBotDecisionEngine Interface

```csharp
namespace Sanet.MakaMek.Core.Models.Game.Players.Bots;

/// <summary>
/// Interface for bot decision-making logic
/// </summary>
public interface IBotDecisionEngine
{
    /// <summary>
    /// Selects the best action for the current game state
    /// </summary>
    Task<IBotAction?> SelectAction(ClientGame game, IPlayer player);
}
```

#### 2.2.2 IBotAction Interface

```csharp
namespace Sanet.MakaMek.Core.Models.Game.Players.Bots;

/// <summary>
/// Represents an action that a bot can take
/// </summary>
public interface IBotAction
{
    /// <summary>
    /// Executes this action by publishing the appropriate command
    /// </summary>
    void Execute(ClientGame game);
    
    /// <summary>
    /// Description of this action for logging/debugging
    /// </summary>
    string Description { get; }
}
```

#### 2.2.3 Phase-Specific Decision Engines

```csharp
namespace Sanet.MakaMek.Core.Models.Game.Players.Bots.DecisionEngines;

// Base implementation
public class BotDecisionEngine : IBotDecisionEngine
{
    private readonly IDeploymentDecisionEngine _deploymentEngine;
    private readonly IMovementDecisionEngine _movementEngine;
    private readonly IWeaponsDecisionEngine _weaponsEngine;
    private readonly IEndPhaseDecisionEngine _endPhaseEngine;
    
    public async Task<IBotAction?> SelectAction(ClientGame game, IPlayer player)
    {
        return game.TurnPhase switch
        {
            PhaseNames.Deployment => await _deploymentEngine.SelectDeploymentAction(game, player),
            PhaseNames.Movement => await _movementEngine.SelectMovementAction(game, player),
            PhaseNames.WeaponsAttack => await _weaponsEngine.SelectWeaponsAction(game, player),
            PhaseNames.End => await _endPhaseEngine.SelectEndPhaseAction(game, player),
            _ => null
        };
    }
}
```

### 2.3 Bot Management

#### 2.3.1 IBotManager Interface

```csharp
namespace Sanet.MakaMek.Core.Models.Game.Players.Bots;

/// <summary>
/// Manages bot players for a game
/// </summary>
public interface IBotManager : IDisposable
{
    /// <summary>
    /// Creates and adds a bot player to the game
    /// </summary>
    Task<IBotPlayer> AddBot(string name, BotDifficulty difficulty, List<UnitData> units);
    
    /// <summary>
    /// Removes a bot player from the game
    /// </summary>
    void RemoveBot(Guid botPlayerId);
    
    /// <summary>
    /// Gets all active bots
    /// </summary>
    IReadOnlyList<IBotPlayer> Bots { get; }
    
    /// <summary>
    /// Starts all bots
    /// </summary>
    Task StartAll();
    
    /// <summary>
    /// Stops all bots
    /// </summary>
    void StopAll();
}
```

#### 2.3.2 BotManager Implementation

```csharp
namespace Sanet.MakaMek.Core.Models.Game.Players.Bots;

public class BotManager : IBotManager
{
    private readonly List<IBotPlayer> _bots = [];
    private readonly IGameFactory _gameFactory;
    private readonly ICommandPublisher _commandPublisher;
    private readonly IBotDecisionEngineFactory _decisionEngineFactory;
    // ... other dependencies
    
    public IReadOnlyList<IBotPlayer> Bots => _bots;
    
    public async Task<IBotPlayer> AddBot(string name, BotDifficulty difficulty, List<UnitData> units)
    {
        // Create a player for the bot
        var player = new Player(Guid.NewGuid(), name, GenerateBotTint());
        
        // Create a ClientGame instance for this bot
        var botClientGame = _gameFactory.CreateClientGame(
            _rulesProvider,
            _mechFactory,
            _commandPublisher,
            _toHitCalculator,
            _pilotingSkillCalculator,
            _consciousnessCalculator,
            _heatEffectsCalculator,
            _mapFactory);
        
        // Join the game with the bot's units
        botClientGame.JoinGameWithUnits(player, units, pilotAssignments);
        
        // Create decision engine for this bot
        var decisionEngine = _decisionEngineFactory.Create(difficulty);
        
        // Create the bot player
        var bot = new BotPlayer(player, botClientGame, decisionEngine, difficulty);
        
        _bots.Add(bot);
        
        return bot;
    }
    
    public async Task StartAll()
    {
        foreach (var bot in _bots)
        {
            await bot.Start();
        }
    }
    
    public void StopAll()
    {
        foreach (var bot in _bots)
        {
            bot.Stop();
        }
    }
    
    // ... other methods
}
```

## 3. ClientGame Instance Decision

### 3.1 Recommendation: One ClientGame Per Bot (Option B)

**Decision: Use one `ClientGame` instance per bot player.**

### 3.2 Justification

#### Pros of One Instance Per Bot:
1. **Isolation**: Each bot has its own game state, preventing interference
2. **Simplicity**: Mirrors the human player model (one ClientGame per human)
3. **LocalPlayers Management**: ClientGame.LocalPlayers naturally maps to one bot
4. **Parallel Processing**: Bots can process commands independently
5. **Easier Debugging**: Each bot's state is isolated and traceable
6. **Future Extensibility**: Supports different bot implementations easily

#### Cons of One Instance Per Bot:
1. **Memory Overhead**: Multiple ClientGame instances consume more memory
2. **Duplicate State**: Each ClientGame maintains a copy of the full game state

#### Why Not Shared Instance (Option A):
1. **LocalPlayers Confusion**: ClientGame.LocalPlayers would contain multiple bot IDs
2. **Action Coordination**: Complex logic needed to determine which bot should act
3. **State Conflicts**: Bots might interfere with each other's decision-making
4. **Tight Coupling**: All bots would depend on a single ClientGame instance

### 3.3 Memory Considerations

The memory overhead is acceptable because:
- ClientGame primarily holds references to shared objects (BattleMap, Players, Units)
- Most game state is already in ServerGame
- Typical games have 2-4 players, so 1-3 bot instances is manageable
- Modern systems can easily handle this overhead

## 4. Communication Flow

### 4.1 Bot Command Flow

```
Bot Decision Engine
    ↓ (selects action)
IBotAction.Execute()
    ↓ (publishes command)
ClientGame.SendPlayerAction()
    ↓
CommandPublisher.PublishCommand()
    ↓
RxTransportPublisher (local transport)
    ↓
ServerGame.HandleCommand()
    ↓ (validates & processes)
ServerGame publishes server command
    ↓
RxTransportPublisher broadcasts
    ↓
All ClientGame instances (human + bot)
    ↓
Bot observes via Commands.Subscribe()
```

### 4.2 Transport Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    ServerGame                            │
│  (Authoritative State, Phase Management)                 │
└────────────────┬────────────────────────────────────────┘
                 │
                 │ CommandPublisher
                 │
    ┌────────────┴────────────┐
    │                         │
    ▼                         ▼
RxTransportPublisher   SignalRTransportPublisher
(Local)                (Network)
    │                         │
    ├─────────┬───────┬───────┼──────────┐
    │         │       │       │          │
    ▼         ▼       ▼       ▼          ▼
ClientGame ClientGame ClientGame  ClientGame  ClientGame
(Human)    (Bot 1)  (Bot 2)   (Remote 1) (Remote 2)
```

## 5. Bot Lifecycle

### 5.1 Creation and Initialization

1. **Pre-Game Setup** (in lobby):
   - User selects "Add Bot" option
   - Specifies bot difficulty and units
   - `BotManager.AddBot()` is called

2. **Bot Creation**:
   - Create `Player` instance with unique ID and name
   - Create `ClientGame` instance via `IGameFactory`
   - Bot's `ClientGame` joins the game via `JoinGameWithUnits()`
   - Create `IBotDecisionEngine` based on difficulty
   - Create `BotPlayer` instance wrapping all components

3. **Server Registration**:
   - Bot's `JoinGameCommand` is processed by `ServerGame`
   - Server creates player and units in authoritative state
   - Other clients receive `JoinGameCommand` and see bot as regular player

### 5.2 Activation and Decision-Making

1. **Game Start**:
   - `BotManager.StartAll()` is called when game begins
   - Each bot calls `Start()`, subscribing to game events

2. **Phase Activation**:
   - `ServerGame` sets active player via `SetActivePlayer()`
   - `ChangeActivePlayerCommand` is broadcast
   - Bot's `ClientGame` receives command and updates `ActivePlayer`
   - Bot's `ActivePlayerChanges` observable fires
   - Bot checks if it's the active player

3. **Decision Process**:
   - Bot adds thinking delay (difficulty-based)
   - `IBotDecisionEngine.SelectAction()` is called
   - Decision engine analyzes current phase and game state
   - Returns appropriate `IBotAction` or null

4. **Action Execution**:
   - `IBotAction.Execute()` publishes command via `ClientGame`
   - Command flows through transport to `ServerGame`
   - Server validates and processes command
   - Server broadcasts result to all clients

### 5.3 Cleanup

1. **Game End**:
   - `BotManager.StopAll()` is called
   - Each bot unsubscribes from events
   - Bot `ClientGame` instances are disposed

2. **Early Removal**:
   - `BotManager.RemoveBot()` can remove individual bots
   - Bot publishes `PlayerLeftCommand`
   - Bot is stopped and disposed

## 6. Action Selection Framework

### 6.1 Phase-Specific Action Discovery

Similar to `UiStates` for human players, bots need phase-specific logic:

#### 6.1.1 Deployment Phase Actions

```csharp
public interface IDeploymentDecisionEngine
{
    Task<IBotAction?> SelectDeploymentAction(ClientGame game, IPlayer player);
}

// Example implementation
public class DeploymentDecisionEngine : IDeploymentDecisionEngine
{
    public async Task<IBotAction?> SelectDeploymentAction(ClientGame game, IPlayer player)
    {
        // Find undeployed units
        var undeployedUnits = player.Units.Where(u => !u.IsDeployed).ToList();
        if (!undeployedUnits.Any()) return null;

        // Select a unit (random for basic bot, strategic for advanced)
        var unit = SelectUnitToDeploy(undeployedUnits);

        // Find valid deployment hexes
        var validHexes = GetValidDeploymentHexes(game.BattleMap, player);

        // Select deployment position
        var hex = SelectDeploymentHex(validHexes, unit);

        // Select facing direction
        var direction = SelectDeploymentFacing(hex, game);

        return new DeployUnitAction(unit.Id, hex.Coordinates, direction);
    }
}
```

#### 6.1.2 Movement Phase Actions

```csharp
public interface IMovementDecisionEngine
{
    Task<IBotAction?> SelectMovementAction(ClientGame game, IPlayer player);
}

// Example implementation
public class MovementDecisionEngine : IMovementDecisionEngine
{
    public async Task<IBotAction?> SelectMovementAction(ClientGame game, IPlayer player)
    {
        // Find units that haven't moved this phase
        var unmoved = player.AliveUnits
            .Where(u => u.IsDeployed && u.MovementTypeUsed == null)
            .ToList();

        if (!unmoved.Any()) return null;

        // Select unit to move
        var unit = SelectUnitToMove(unmoved);

        // Determine movement type (walk, run, jump, stand still)
        var movementType = SelectMovementType(unit, game);

        if (movementType == MovementType.StandingStill)
        {
            return new MoveUnitAction(unit.Id, movementType, []);
        }

        // Calculate movement path
        var path = CalculateMovementPath(unit, movementType, game);

        return new MoveUnitAction(unit.Id, movementType, path);
    }
}
```

#### 6.1.3 Weapons Phase Actions

```csharp
public interface IWeaponsDecisionEngine
{
    Task<IBotAction?> SelectWeaponsAction(ClientGame game, IPlayer player);
}

// Example implementation
public class WeaponsDecisionEngine : IWeaponsDecisionEngine
{
    public async Task<IBotAction?> SelectWeaponsAction(ClientGame game, IPlayer player)
    {
        // Find units that haven't declared attacks
        var unattacked = player.AliveUnits
            .Where(u => !u.HasDeclaredWeaponAttack && u.CanFireWeapons)
            .ToList();

        if (!unattacked.Any()) return null;

        // Select attacker
        var attacker = SelectAttacker(unattacked);

        // Find potential targets
        var targets = FindPotentialTargets(attacker, game);

        if (!targets.Any())
        {
            // Skip attack if no targets
            return new SkipWeaponAttackAction(attacker.Id);
        }

        // Select target
        var target = SelectTarget(targets, attacker, game);

        // Select weapons to fire
        var weapons = SelectWeapons(attacker, target, game);

        return new DeclareWeaponAttackAction(attacker.Id, target.Id, weapons);
    }
}
```

#### 6.1.4 End Phase Actions

```csharp
public interface IEndPhaseDecisionEngine
{
    Task<IBotAction?> SelectEndPhaseAction(ClientGame game, IPlayer player);
}

// Example implementation
public class EndPhaseDecisionEngine : IEndPhaseDecisionEngine
{
    public async Task<IBotAction?> SelectEndPhaseAction(ClientGame game, IPlayer player)
    {
        // Check for shutdown units that should attempt restart
        var shutdownUnits = player.AliveUnits.Where(u => u.IsShutdown).ToList();

        foreach (var unit in shutdownUnits)
        {
            if (ShouldAttemptRestart(unit, game))
            {
                return new StartupUnitAction(unit.Id);
            }
        }

        // Check for overheated units that should shutdown
        var overheatedUnits = player.AliveUnits
            .Where(u => u.IsActive && ShouldShutdown(u))
            .ToList();

        if (overheatedUnits.Any())
        {
            var unit = overheatedUnits.First();
            return new ShutdownUnitAction(unit.Id);
        }

        // End turn
        return new EndTurnAction(player.Id);
    }
}
```

### 6.2 Bot Action Implementations

```csharp
// Example action implementations
public class DeployUnitAction : IBotAction
{
    private readonly Guid _unitId;
    private readonly HexCoordinates _position;
    private readonly HexDirection _facing;

    public string Description => $"Deploy unit {_unitId} at {_position} facing {_facing}";

    public void Execute(ClientGame game)
    {
        var command = new DeployUnitCommand
        {
            GameOriginId = game.Id,
            PlayerId = game.ActivePlayer!.Id,
            UnitId = _unitId,
            Position = _position,
            Facing = _facing
        };
        game.DeployUnit(command);
    }
}

public class MoveUnitAction : IBotAction
{
    private readonly Guid _unitId;
    private readonly MovementType _movementType;
    private readonly List<PathSegmentData> _path;

    public string Description => $"Move unit {_unitId} using {_movementType}";

    public void Execute(ClientGame game)
    {
        var command = new MoveUnitCommand
        {
            GameOriginId = game.Id,
            PlayerId = game.ActivePlayer!.Id,
            UnitId = _unitId,
            MovementType = _movementType,
            MovementPath = _path
        };
        game.MoveUnit(command);
    }
}

public class DeclareWeaponAttackAction : IBotAction
{
    private readonly Guid _attackerId;
    private readonly Guid _targetId;
    private readonly List<WeaponSelectionData> _weapons;

    public string Description => $"Attack {_targetId} with {_weapons.Count} weapons";

    public void Execute(ClientGame game)
    {
        var command = new WeaponAttackDeclarationCommand
        {
            GameOriginId = game.Id,
            PlayerId = game.ActivePlayer!.Id,
            AttackerId = _attackerId,
            TargetId = _targetId,
            WeaponSelections = _weapons
        };
        game.DeclareWeaponAttack(command);
    }
}

public class EndTurnAction : IBotAction
{
    private readonly Guid _playerId;

    public string Description => $"End turn for player {_playerId}";

    public void Execute(ClientGame game)
    {
        var command = new TurnEndedCommand
        {
            GameOriginId = game.Id,
            PlayerId = _playerId
        };
        game.EndTurn(command);
    }
}

// ... similar implementations for other actions
```

## 7. Integration Points

### 7.1 GameManager Integration

`GameManager` should be extended to support bot management:

```csharp
public class GameManager : IGameManager
{
    private readonly IBotManager _botManager;

    public async Task InitializeLobby()
    {
        // ... existing code ...

        // Initialize bot manager
        _botManager = new BotManager(
            _gameFactory,
            _commandPublisher,
            _rulesProvider,
            _mechFactory,
            // ... other dependencies
        );
    }

    public async Task<IBotPlayer> AddBot(string name, BotDifficulty difficulty, List<UnitData> units)
    {
        return await _botManager.AddBot(name, difficulty, units);
    }

    public void SetBattleMap(BattleMap battleMap)
    {
        _serverGame?.SetBattleMap(battleMap);

        // Start bots when game begins
        _ = Task.Run(() => _botManager.StartAll());
    }
}
```

### 7.2 StartNewGameViewModel Integration

UI should allow adding bots in the lobby:

```csharp
public class StartNewGameViewModel : NewGameViewModel
{
    private readonly IGameManager _gameManager;

    public ICommand AddBotCommand => new AsyncCommand(async () =>
    {
        var botName = $"Bot {_players.Count + 1}";
        var difficulty = BotDifficulty.Medium; // Could be user-selected
        var units = SelectBotUnits(); // UI for selecting bot units

        var bot = await _gameManager.AddBot(botName, difficulty, units);

        // Bot automatically joins the game, will appear in player list
        // via JoinGameCommand handling
    });
}
```

### 7.3 Dependency Injection

Register bot-related services:

```csharp
// In DI configuration
services.AddTransient<IBotManager, BotManager>();
services.AddTransient<IBotDecisionEngineFactory, BotDecisionEngineFactory>();
services.AddTransient<IDeploymentDecisionEngine, DeploymentDecisionEngine>();
services.AddTransient<IMovementDecisionEngine, MovementDecisionEngine>();
services.AddTransient<IWeaponsDecisionEngine, WeaponsDecisionEngine>();
services.AddTransient<IEndPhaseDecisionEngine, EndPhaseDecisionEngine>();
```

## 8. Potential Challenges

### 8.1 Architectural Gaps

1. **Initiative Phase**: Bots need to automatically roll initiative
   - **Solution**: Add `IInitiativeDecisionEngine` that publishes `RollDiceCommand` when bot is active player

2. **Piloting Skill Rolls**: Bots need to handle PSR prompts automatically
   - **Solution**: Server should auto-roll for bots (check if player is bot), or bot observes PSR-required events and responds

3. **Standup Attempts**: Bots need to decide when to attempt standing up
   - **Solution**: Include in `IMovementDecisionEngine` logic - check if unit is prone and decide whether to standup

4. **Heat Management**: Bots need to make shutdown/startup decisions
   - **Solution**: Implemented in `IEndPhaseDecisionEngine` with heat threshold logic

5. **Torso Twisting**: Bots need to decide when to rotate torso
   - **Solution**: Include in `IWeaponsDecisionEngine` - evaluate if torso rotation improves firing arcs

### 8.2 Timing and Synchronization

1. **Race Conditions**: Multiple bots might try to act simultaneously
   - **Solution**: Each bot has its own `ClientGame`, server serializes commands via phase management

2. **Thinking Delays**: Bots should not respond instantly (feels unnatural)
   - **Solution**: Implemented via `GetThinkingDelay()` based on difficulty level

3. **Command Validation**: Bot commands might be invalid due to state changes
   - **Solution**: Server validates all commands; bot should handle failures gracefully (retry or skip)

4. **Phase Transitions**: Bot might be deciding when phase changes
   - **Solution**: Bot checks `CanActivePlayerAct` before executing; server rejects stale commands

### 8.3 State Management

1. **Game State Synchronization**: Bots must maintain accurate game state
   - **Solution**: `ClientGame` already handles this via command observation; bots leverage existing mechanism

2. **Unit State Tracking**: Bots need to track which units have acted
   - **Solution**: Use existing `Unit` properties (MovementTypeUsed, HasDeclaredWeaponAttack, IsDeployed, etc.)

3. **Partial Information**: Bots should only know what their player knows
   - **Solution**: Bot uses `ClientGame` which only has information broadcast by server (no cheating)

### 8.4 Testing Challenges

1. **Bot Behavior Testing**: Difficult to test decision-making logic in isolation
   - **Solution**: Mock `ClientGame` and verify correct commands are published for given game states

2. **Integration Testing**: Testing bots with real games is complex
   - **Solution**: Create integration tests with simplified scenarios (small maps, few units)

3. **Performance Testing**: Multiple bots might impact performance
   - **Solution**: Profile and optimize decision engines; consider async decision-making

4. **Determinism**: Bot behavior should be testable and reproducible
   - **Solution**: Allow seeding random number generators for tests

## 9. Future Extensibility

### 9.1 Difficulty Levels

The design supports multiple difficulty implementations:

- **Easy**:
  - Random valid actions
  - Basic target selection (closest enemy)
  - No heat management
  - Simple movement (random valid hex)

- **Medium**:
  - Considers range and line of sight
  - Heat-aware weapon selection
  - Basic damage potential calculation
  - Cover-seeking movement

- **Hard**:
  - Advanced tactics (flanking, focus fire)
  - Optimal positioning and range management
  - Heat management and alpha strikes
  - Target prioritization (damaged units, high-value targets)
  - Predictive movement (anticipate enemy positions)

### 9.2 AI Strategies

Different bot personalities can be implemented via strategy pattern:

- **Aggressive**:
  - Prioritizes offense over defense
  - Closes distance quickly
  - Uses run/jump frequently
  - Accepts heat buildup for damage

- **Defensive**:
  - Maintains optimal range
  - Uses cover and terrain
  - Conservative heat management
  - Focuses on survival

- **Balanced**:
  - Mix of offense and defense
  - Adapts to situation
  - Moderate risk-taking

- **Sniper**:
  - Long-range focus
  - Minimal movement
  - Precision targeting
  - Avoids close combat

### 9.3 Learning and Adaptation

Future enhancements could include:

- **Machine Learning**: Train bots on game outcomes using reinforcement learning
- **Adaptive Difficulty**: Adjust bot difficulty based on player win/loss ratio
- **Strategy Patterns**: Recognize and counter player tactics
- **Meta-Learning**: Bots learn from multiple games to improve decision-making

### 9.4 Multiplayer Scenarios

The architecture supports various multiplayer configurations:

- **Mixed Games**: Human + Bot + Remote players in same game
- **Bot vs Bot**: Fully automated games for testing and simulation
- **Bot Replacement**: Replace disconnected human players with bots mid-game
- **Team Play**: Bots coordinate with human teammates
- **Training Mode**: Bots provide practice opponents for new players

## 10. Implementation Phases

### Phase 1: Core Infrastructure (Week 1-2)
- [ ] Implement `IBotPlayer`, `BotPlayer` classes
- [ ] Implement `IBotManager`, `BotManager` classes
- [ ] Implement `IBotAction` interface and base action classes
- [ ] Integrate `BotManager` with `GameManager`
- [ ] Add bot-related dependency injection configuration
- [ ] Unit tests for core bot infrastructure

### Phase 2: Basic Decision Engines (Week 3-4)
- [ ] Implement `IDeploymentDecisionEngine` with random deployment logic
- [ ] Implement `IMovementDecisionEngine` with random movement logic
- [ ] Implement `IWeaponsDecisionEngine` with random target selection
- [ ] Implement `IEndPhaseDecisionEngine` with basic end turn logic
- [ ] Implement `IInitiativeDecisionEngine` for initiative rolls
- [ ] Unit tests for each decision engine

### Phase 3: UI Integration (Week 5)
- [ ] Add "Add Bot" button to `StartNewGameViewModel`
- [ ] Display bots in player list with bot indicator
- [ ] Bot difficulty selection UI
- [ ] Bot unit selection UI
- [ ] Remove bot functionality
- [ ] Integration tests for UI workflows

### Phase 4: Enhanced AI (Week 6-8)
- [ ] Improve deployment logic (strategic positioning, formation)
- [ ] Improve movement logic (pathfinding, cover-seeking, range optimization)
- [ ] Improve weapons logic (target prioritization, heat management, weapon selection)
- [ ] Add difficulty variations (Easy, Medium, Hard)
- [ ] Implement thinking delays
- [ ] Performance optimization

### Phase 5: Testing and Polish (Week 9-10)
- [ ] Comprehensive integration testing (full game scenarios)
- [ ] Performance profiling and optimization
- [ ] Bug fixes and edge case handling
- [ ] Documentation (code comments, user guide)
- [ ] Playtesting and balance adjustments

## 11. Success Criteria

1. **Functional**: Bots can complete full games from deployment to victory/defeat without errors
2. **Network Transparent**: Remote players cannot distinguish bots from human players via network traffic
3. **Performance**: Bots do not cause noticeable lag or performance degradation
4. **Usability**: Users can easily add/remove bots from lobby with intuitive UI
5. **Extensibility**: New bot strategies and difficulty levels can be added without major refactoring
6. **Reliability**: Bots handle edge cases gracefully (invalid commands, state changes, etc.)
7. **Testability**: Bot behavior is testable and reproducible

## 12. Risks and Mitigations

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Bot commands frequently invalid | High | Medium | Comprehensive validation in decision engines; extensive testing |
| Performance degradation with multiple bots | Medium | Low | Profile and optimize; limit bot count; async decision-making |
| Complex decision logic hard to implement | High | High | Start simple (random), iterate based on testing; modular design |
| State synchronization issues | High | Low | Leverage existing `ClientGame` mechanisms; thorough testing |
| Testing difficulty | Medium | Medium | Create comprehensive test scenarios; mock frameworks |
| Bot behavior feels unnatural | Medium | Medium | Add thinking delays; vary actions; playtesting |
| Initiative/PSR handling gaps | Medium | Medium | Implement auto-roll for bots; server-side detection |

## 13. Alternative Approaches Considered

### 13.1 Server-Side Bot Logic (Rejected)

**Approach**: Implement bot decision-making directly in `ServerGame` without using `ClientGame`.

**Pros**:
- No need for bot `ClientGame` instances
- Direct access to authoritative state
- Potentially simpler implementation

**Cons**:
- Violates client-server separation
- Bots would have perfect information (cheating)
- Harder to test in isolation
- Network transparency compromised
- Doesn't reuse existing client infrastructure

**Verdict**: Rejected - violates architectural principles

### 13.2 Shared ClientGame for All Bots (Rejected)

**Approach**: Use one `ClientGame` instance for all bots with `LocalPlayers` containing all bot IDs.

**Pros**:
- Lower memory overhead
- Single game state for all bots

**Cons**:
- Complex coordination logic needed
- `LocalPlayers` semantics unclear
- Tight coupling between bots
- Harder to debug and test
- Doesn't scale well

**Verdict**: Rejected - too complex and fragile

### 13.3 Bot as UI State (Rejected)

**Approach**: Implement bots as automated UI states that control a human player's `ClientGame`.

**Pros**:
- Reuses existing UI state infrastructure
- Minimal new code

**Cons**:
- Bots tied to presentation layer
- Can't run headless
- Doesn't work for server-hosted bots
- Violates separation of concerns

**Verdict**: Rejected - wrong architectural layer

## 14. Conclusion

The proposed bot player system integrates seamlessly with MakaMek's existing client-server architecture by:

1. **Reusing ClientGame**: Each bot uses its own `ClientGame` instance (one per bot), mirroring the human player model
2. **RX Transport**: Bots communicate via `RxTransportPublisher` like local human players, ensuring network transparency
3. **Server-Side Hosting**: Bot `ClientGame` instances are created and managed server-side, invisible to remote players
4. **Phase-Specific Decision Engines**: Similar to UI states, bots use specialized decision engines for each game phase
5. **Command Pattern**: Bots publish commands through `ClientGame`, validated by `ServerGame` like any other player
6. **Extensible Design**: Supports multiple difficulty levels, strategies, and future AI enhancements

### Key Architectural Strengths

- **Separation of Concerns**: Bot logic is separate from UI, server, and transport layers
- **Testability**: Each component can be tested in isolation with mocks
- **Maintainability**: Modular design allows easy updates and extensions
- **Consistency**: Bots use the same mechanisms as human players
- **Network Transparency**: Remote players see bots as regular players

### Implementation Readiness

The design is ready for implementation with:
- Clear component interfaces and responsibilities
- Well-defined integration points
- Phased implementation plan
- Identified risks and mitigations
- Success criteria and testing strategy

This architecture provides a solid foundation for AI opponents in MakaMek, enabling engaging single-player and mixed multiplayer experiences while maintaining the integrity of the existing client-server design.




