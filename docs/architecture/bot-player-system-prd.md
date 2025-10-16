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

1. **Reusing ClientGame**: Bots use a `ClientGame` instance to interact with the server
2. **BotPlayers List**: All bot player IDs added to `ClientGame.BotPlayers`
3. **RX Transport**: Bots communicate via `RxTransportPublisher`, identical to local human players
5. **Network Transparency**: Remote players cannot distinguish bots from human players
6. **Decision Engine**: Bot logic observes game state and publishes appropriate commands

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
    
    // depends on current phase, the same as UIState for human players
    privte IBotDecisionEngine _currentDecisionEngine,
    
    public IPlayer Player { get; }
    public BotDifficulty Difficulty { get; }
    
    public BotPlayer(
        IPlayer player,
        ClientGame clientGame,
        BotDifficulty difficulty)
    {
        Player = player;
        ClientGame = clientGame;
        _decisionEngine = decisionEngine;
        Difficulty = difficulty;
        
        _commandSubscription = ClientGame.Commands.Subscribe(OnCommandReceived);
    }
    
    private void OnCommandReceived(IGameCommand command)
    {
        // Bot observes all commands to maintain awareness of game state
        // If a command activates the bot's player, it should make a decision(s) corresponding to current game phase
    }
    
    private int GetThinkingDelay()
    {
        // optionally introduce delay's to Bot's decision making to make it feel more natural
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

### 2.2 Decision Engine

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
    private readonly IDecisionEngine _deploymentEngine;
    private readonly IDecisionEngine _movementEngine;
    private readonly IDecisionEngine _weaponsEngine;
    private readonly IDecisionEngine _endPhaseEngine;
    
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
    Task<IBotPlayer> AddBot(IPlayer, BotDifficulty difficulty);
    
    /// <summary>
    /// Removes a bot player from the game
    /// </summary>
    void RemoveBot(Guid botPlayerId);
    
    /// <summary>
    /// Gets all active bots
    /// </summary>
    IReadOnlyList<IBotPlayer> Bots { get; }
}
```

#### 2.3.2 BotManager Implementation

```csharp
public class BotManager : IBotManager
{
    private readonly List<IBotPlayer> _bots = [];
    private readonly ClientGame _clientGame; // how to set it? Reset() method accepting the client game and cleaning up all the current bots?
    private readonly IBotDecisionEngineFactory _decisionEngineFactory;
    // ... other dependencies
    
    public IReadOnlyList<IBotPlayer> Bots => _bots;
    
    public async Task<IBotPlayer> AddBot(IPlayer player, BotDifficulty difficulty)
    {
                
        // Join the game with the bot's units
        _clientGame.JoinGameWithUnits(player, units, pilotAssignments); //+ a flag indicating it's a bot
        
        // Create decision engine for this bot
        var decisionEngine = _decisionEngineFactory.Create(difficulty);
        
        // Create the bot player
        var bot = new BotPlayer(player, clientGame, decisionEngine, difficulty);
        
        _bots.Add(bot);
        
        return bot;
    }
    
    // ... other methods
}
```

Bots could be added to existing client games (created in either `StartNewGameViewModel` or `JoinGameViewModel`) by levaraging existing JoinGameWithUnits method passing `IsBot` flag. ClientGame would add those players to `BotPlayers` instead of `LocalPlayers`.

The open question is where to "host" the BotManager itself.

## 3. ClientGame Instance Decision

### 3.1 Recommendation: One Shared ClientGame for All Bots and Human Players on that "client" device

**Decision: Use one `ClientGame` instance for all bots, with all bot player IDs in `BotPlayers`.**

### 3.2 Justification

#### Pros of Shared ClientGame:
1. **Mirrors Current Architecture**: Exactly how local human players work
2. **Single Game State**: One synchronized state for all bots 
3. **Lower Memory Overhead**: One `ClientGame` instance instead of N
4. **Simpler Transport**: One subscription to command stream
5. **Consistent with Design**: Follows existing patterns
6. **Turn-Based Game**: Bots act sequentially when activated, no parallel processing needed

#### Why Not One Instance Per Bot:
1. **Violates Current Architecture**: Human players share one `ClientGame`, bots should too
2. **Higher Memory Overhead**: N duplicate game states
3. **Inconsistent Design**: Bots would work differently than humans
4. **Wrong Conceptually**: Game state is shared, not per-player - there's no "bot state" vs "human state"
5. **Unnecessary Complexity**: Turn-based game doesn't need parallel bot processing

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
All ClientGame instances (human + bot + remote)
    ↓
Bot's ClientGame observes via Commands.Subscribe()
```

### 4.2 Transport Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    ServerGame                           │
│  (Authoritative State, Phase Management)                │
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
    │                         ├──────────┐
    │                         │          │
    ▼                         ▼          ▼
ClientGame              ClientGame  ClientGame
(Humans & Bots)          (Remote 1) (Remote 2)
    │                        │         
    ├─────────┐              ├─────────┐
    │         │              │         │ 
    ▼         ▼              ▼         ▼
   UI      BotManager        UI      BotManager
```

## 5. Bot Lifecycle

### 5.1 Creation and Initialization

1. **Pre-Game Setup** (in lobby):
   - `BitManager` is initialized with ClientGame instance as soon as it's created
   - User selects "Add Bot" option
   - Specifies bot difficulty (optional for now) and units
   - Create `Player` instance with unique ID and name (follows current pattern)
   - `BotManager.AddBot()` is called

2. **Bot Creation**:
   - Bot's player ID is added to `ClientGameBotsPlayers` via `JoinGameWithUnits()`
   - Create `IBotDecisionEngine` based on difficulty
   - Create `BotPlayer` instance
   - Add to `BotManager.Bots` list

3. **Server Registration**:
   - Bot's `JoinGameCommand` is processed by `ServerGame`
   - Server creates player and units in authoritative state, no difference from regular human players
   - Other clients receive `JoinGameCommand` and see bot as regular player

### 5.2 Activation and Decision-Making

1. **Game Start**:
   - `BotPlayer` is already subscribed to `ClientGame` commands

2. **Phase Activation**:
   - `ServerGame` sets active player via `ChangeActivePlayerCommand`
   - Bot's `ClientGame` receives command and updates `ActivePlayer`
   - `BotPlayer` observes command and acts if ActivePlayer's Id matches its player Id

3. **Decision Process**:
   - Decision engine analyzes current phase and game state
   - Returns appropriate `IBotAction`
   - Bot Should always execute an action, even if it's a "skip" one to prvent game stuck 

4. **Action Execution**:
   - `IBotAction.Execute()` publishes command via `ClientGame`
   - Command flows through transport to `ServerGame`
   - Server validates and processes command
   - Server broadcasts result to all clients

### 5.3 Cleanup

1. **Game End**:
   - Each bot unsubscribes from events
   - Bot `ClientGame` instances are disposed
   - BotPlayers are cleared/disposed on next game start

## 6. Action Selection Framework

### 6.1 Phase-Specific Action Discovery

Similar to `UiStates` for human players, bots need phase-specific logic. Some bits of logic could be extracted and shared between specific UIStates and DecisionEngines if/when makes sense.

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

Ideally Bot Framework should be developed as a separate project/dll with only dependency on MakaMek.Core.
That would force correct DI direction and mitigate risk of messy code/architectural decisions.  

### 7.1 StartNewGameViewModel/JoinGameViewModel Integration

UI should allow adding bots in the lobby, reuse current AddPlayer logic, extending it to add Bots via the BotManager, not directly to the ClientGame.

### 7.2 Dependency Injection

Register bot-related services:

```csharp
// In DI configuration
services.AddTransient<IBotManager, BotManager>();
services.AddTransient<IBotDecisionEngineFactory, BotDecisionEngineFactory>();
services.AddTransient<IDecisionEngine, DeploymentDecisionEngine>(); // just a pseudo code, that won't work with the same interface like that
services.AddTransient<IDecisionEngine, MovementDecisionEngine>();
services.AddTransient<IDecisionEngine, WeaponsDecisionEngine>();
services.AddTransient<IDecisionEngine, EndPhaseDecisionEngine>();
```

## 8. Potential Challenges

1. **Standup Attempts**: Bots need to decide when to attempt standing up
   - **Solution**: Include in `IMovementDecisionEngine` logic - check if unit is prone and decide whether to standup

2  **Heat Management**: Bots need to make shutdown/startup decisions
   - **Solution**: Implemented in `IEndPhaseDecisionEngine` with heat threshold logic

3. **Torso Twisting**: Bots need to decide when to rotate torso
   - **Solution**: Include in `IWeaponsDecisionEngine` - evaluate if torso rotation improves firing arcs
   - 
4. **Thinking Delays**: Bots should not respond instantly (feels unnatural)
   - **Solution**: Implemented via `GetThinkingDelay()` based on difficulty level
   
5. **Unit State Tracking**: Bots need to track which units have acted
   - **Solution**: Use existing `Unit` properties (MovementTypeUsed, HasDeclaredWeaponAttack, IsDeployed, etc.)

6. **Partial Information**: Bots should only know what their player knows
   - **Solution**: Bot uses `ClientGame` which only has information broadcast by server (no cheating)

7. **Bot Behavior Testing**: Difficult to test decision-making logic in isolation
   - **Solution**: Mock `ClientGame` and verify correct commands are published for given game states

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

### Phase 1: Core Infrastructure 
- [ ] Implement `IBotPlayer`, `BotPlayer` classes
- [ ] Implement `IBotManager`, `BotManager` classes
- [ ] Implement `IBotAction` interface and base action classes
- [ ] Integrate `BotManager` with ClientGame
- [ ] Add bot-related dependency injection configuration
- [ ] Unit tests for core bot infrastructure

### Phase 2: Basic Decision Engines 
- [ ] Implement `DeploymentDecisionEngine` with random deployment logic
- [ ] Implement `MovementDecisionEngine` with random movement logic
- [ ] Implement `WeaponsDecisionEngine` with random target selection
- [ ] Implement `EndPhaseDecisionEngine` with basic end turn logic
- [ ] Unit tests for each decision engine

### Phase 3: UI Integration 
- [ ] Add "Add Bot" button to `StartNewGameViewModel`
- [ ] Display bots in player list with bot indicator
- [ ] Remove bot functionality

### Phase 4: Enhanced AI 
- [ ] Improve deployment logic (strategic positioning, formation)
- [ ] Improve movement logic (pathfinding, cover-seeking, range optimization)
- [ ] Improve weapons logic (target prioritization, heat management, weapon selection)
- [ ] Implement thinking delays
- [ ] Performance optimization

## 11. Success Criteria

1. **Functional**: Bots can complete full games from deployment to victory/defeat without errors
2. **Network Transparent**: Remote players cannot distinguish bots from human players via network traffic
3. **Performance**: Bots do not cause noticeable lag or performance degradation
4. **Usability**: Users can easily add/remove bots from lobby with intuitive UI
5. **Extensibility**: New bot strategies and difficulty levels can be added without major refactoring
6. **Reliability**: Bots handle edge cases gracefully (invalid commands, state changes, etc.)
7. **Testability**: Bot behavior is testable and reproducible

## 12. Conclusion

The proposed bot player system integrates seamlessly with MakaMek's existing client-server architecture by:

1. **Reusing ClientGame**: Each bot uses a `ClientGame` instance, mirroring the human player model
2. **RX Transport**: Bots communicate via `RxTransportPublisher` like local human players, ensuring network transparency
3. **Phase-Specific Decision Engines**: Similar to UI states, bots use specialized decision engines for each game phase
4. **Command Pattern**: Bots publish commands through `ClientGame`, validated by `ServerGame` like any other player
5. **Extensible Design**: Supports multiple difficulty levels, strategies, and future AI enhancements

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




