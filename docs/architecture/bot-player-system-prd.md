# Bot Player System - Product Requirements Document
**Date:** 2025-10-16  
**Status:** Ready for Implementation

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
2. **BotPlayers List**: All bot player IDs added to `ClientGame.BotPlayers` (doesn't exist yet)
3. **RX Transport**: Bots communicate via `RxTransportPublisher`, identical to local human players
5. **Network Transparency**: Remote players cannot distinguish bots from human players
6. **Decision Engine**: Bot logic observes game state and publishes appropriate commands

## 2. Component Design

### 2.1 Core Bot Components

#### 2.1.1 IBot Interface

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

#### 2.1.2 Bot Implementation

```csharp
public class Bot : IBot
{
    private IBotDecisionEngine _decisionEngine;
    private IDisposable? _commandSubscription;
    
    // depends on current phase, stateless, the same as UIState for human players
    private IBotDecisionEngine _currentDecisionEngine;
    
    public IPlayer Player { get; }
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
            _currentDecisionEngine = TransferDecisionEngine(phaseCmd.Phase);
            break;
        case GameEndedCommand:
            Dispose();
            break;
        // Ignore other commands
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

#### 2.2.2 Phase-Specific Decision Engines

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
Other engines follow the same pattern

### 2.3 Bot Management

#### 2.3.1 IBotManager Interface

```csharp
/// <summary>
/// Manages/hosts bot players for a game
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
    void AddBot(IPlayer, BotDifficulty difficulty);
    
    /// <summary>
    /// Removes a bot player from the game
    /// </summary>
    void RemoveBot(Guid BotId);
    
    /// <summary>
    /// Gets all active bots
    /// </summary>
    IReadOnlyList<IBot> Bots { get; }
}
```

#### 2.3.2 BotManager Implementation

```csharp
public class BotManager : IBotManager
{
    private readonly List<IBot> _bots = [];
    private ClientGame? _clientGame;
    // ... other dependencies
    
    public void Initialize(ClientGame clientGame)
    {
        _clientGame = clientGame;
        foreach (var bot in _bots)
        {
            bot.Dispose();
        }
        _bots.Clear();
    }
    
    public IReadOnlyList<IBot> Bots => _bots;
    
    public void AddBot(IPlayer player, BotDifficulty difficulty = BotDifficulty.Easy)
    {
        if (ClientGame == null) return; //Introduce a better way to handle this, maybe IsInitialized property
        // Join the game with the bot's units
        _clientGame.JoinGameWithUnits(player, units, pilotAssignments); //+ a flag indicating it's a bot
        
        // Create the bot player
        var bot = new Bot(player, _clientGame, difficulty);
        
        _bots.Add(bot);
    }
    
    // ... other methods
}
```

Bots could be added to existing client games (created in either `StartNewGameViewModel` or `JoinGameViewModel`) by levaraging existing JoinGameWithUnits method passing `IsBot` flag. ClientGame would add those players to `Bots` instead of `LocalPlayers`.

The open question is where to "host" the BotManager itself, it might be just a singleton per client device.

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
1. **Higher Memory Overhead**: N duplicate game states
2. **Inconsistent Design**: Bots would work differently than humans
3. **Wrong Conceptually**: Game state is shared, not per-player - there's no "bot state" vs "human state"
4. **Unnecessary Complexity**: Turn-based game doesn't need parallel bot processing

## 4. Communication Flow

### 4.1 Bot Command Flow

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
   - `BotManager` is initialized with ClientGame instance as soon as it's created
   - User selects "Add Bot" option
   - Specifies bot difficulty (optional for now) and units
   - Create `Player` instance with unique ID and name (follows current pattern)
   - `BotManager.AddBot()` is called

2. **Bot Creation**:
   - Bot's player ID is added to `ClientGame.BotPlayers` via `JoinGameWithUnits()`
   - Create `IBotDecisionEngine` based on difficulty
   - Create `Bot` instance
   - Add to `BotManager.Bots` list

3. **Server Registration**:
   - Bot's `JoinGameCommand` is processed by `ServerGame`
   - Server creates player and units in authoritative state, no difference from regular human players
   - Other clients receive `JoinGameCommand` and see bot as regular player

### 5.2 Activation and Decision-Making

1. **Game Start**:
   - `Bot` is already subscribed to `ClientGame` commands

2. **Phase Activation**:
   - `ServerGame` transitions to first phase (Deployment)
   - Bot's `ClientGame` receives `ChangePhaseCommand` and sets correct DecisionEngine
   - `ServerGame` sets active player via `ChangeActivePlayerCommand`
   - Bot's `ClientGame` receives command and updates `ActivePlayer`
   - `Bot` observes command and acts if ActivePlayer's Id matches its player Id

3. **Decision Process**:
   - Decision engine analyzes current phase and game state
   - Bot Should always execute an action, even if it's a "skip" one to prevent game stuck, `try action, finally skip` pattern 

4. **Action Execution**:
   - `DecisionEngine` publishes command via `ClientGame`
   - Command flows through transport to `ServerGame`
   - Server validates and processes command
   - Server broadcasts result to all clients

### 5.3 Cleanup

1. **Game End**:
   - Each bot unsubscribes from events
   - Bot `ClientGame` instances are disposed
   - Bots are cleared/disposed on next game start

## 6. Action Selection Framework

### 6.1 Phase-Specific Action Discovery

Similar to `UiStates` for human players, bots need phase-specific logic. Some bits of logic could be extracted and shared between specific UIStates and DecisionEngines if/when makes sense.

#### 6.1.1 Deployment Phase Actions
Possible implementation pseudo code:

```csharp
// Example implementation
public class DeploymentEngine : IDecisionEngine
{
    private readonly ClientGame _game;
    private readonly IPlayer _player;
    
    public DeploymentDecisionEngine(ClientGame game, IPlayer player)
    {
        _game = game;
        _player = player;
    }
    
    public async Task MakeDecision()
    {
        try
        {
            // Find undeployed units
            var undeployedUnits = player.Units.Where(u => !u.IsDeployed).ToList();
            if (!undeployedUnits.Any()) throw new Exception("No undeployed units");
    
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
        finally
        {
            // Always skip turn if no action taken
            await SkipTurn();
        }
        
    }
}
```

#### 6.1.2 Movement Phase Actions

```csharp
// Example implementation
public class MovementEngine : IDecisionEngine
{
    private readonly ClientGame _game;
    private readonly IPlayer _player;
    
    public MovementDecisionEngine(ClientGame game, IPlayer player)
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
            if (!unmoved.Any()) throw new Exception("No unmoved units");
            
            // Select unit to move
            var unit = SelectUnitToMove(unmoved);
    
            // Determine movement type (walk, run, jump, stand still)
            var movementType = SelectMovementType(unit, game);
            
            // Calculate movement path
            var path = CalculateMovementPath(unit, movementType, game);
            _game.MoveUnit(new MoveUnitCommand  // existing builder can be used too
            {
                GameOriginId = _game.Id,
                PlayerId = _player.Id,
                UnitId = unit.Id,
                MovementType = movementType,
                MovementPath = path
            });
        }
        finally
        {
            // Always skip turn if no action taken
            await SkipTurn();
        };
    }
}
```

#### 6.1.3 Weapons Phase Actions

```csharp
// Example implementation
public class WeaponsDecisionEngine : IDecisionEngine
{
    private readonly ClientGame _game;
    private readonly IPlayer _player;
    
    public WeaponsDecisionEngine(ClientGame game, IPlayer player)
    {
        _game = game;
        _player = player;
    }
    
    public async Task MakeDecision()
    {
        try
        {
            // Find units that haven't declared attacks
            var unattacked = player.AliveUnits
                .Where(u => !u.HasDeclaredWeaponAttack && u.CanFireWeapons)
                .ToList();
    
            if (!unattacked.Any()) throw new Exception("No units to attack");
    
            // Select attacker
            var attacker = SelectAttacker(unattacked);
    
            // Find potential targets
            var targets = FindPotentialTargets(attacker, game);
    
            if (!targets.Any())
            {
                // Skip attack if no targets
                SkipTurn();
            }
    
            // Select target
            var target = SelectTarget(targets, attacker, game);
    
            // Select weapons to fire
            var weapons = SelectWeapons(attacker, target, game);
    
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
        finally
        {
            // Always skip turn if no action taken
            await SkipTurn();
        }
    }
}
```

#### 6.1.4 End Phase Actions

```csharp
// Example implementation
public class EndPhaseDecisionEngine : IDecisionEngine
{
    private readonly ClientGame _game;
    private readonly IPlayer _player;
    
    public EndPhaseDecisionEngine(ClientGame game, IPlayer player)
    {
        _game = game;
        _player = player;
    }
    
    public async Task MakeDecision()
    {
        try
        {
            // Check for shutdown units that should attempt restart
            var shutdownUnits = player.AliveUnits.Where(u => u.IsShutdown).ToList();
    
            foreach (var unit in shutdownUnits)
            {
                if (ShouldAttemptRestart(unit, game))
                {
                    StartupUnit(unit.Id);
                }
            }
    
            // Check for overheated units that should shutdown
            var overheatedUnits = player.AliveUnits
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
            // End turn
            return new EndTurnAction(player.Id);
        }
    }
}
```

## 7. Integration Points

Bot Framework should be developed as a separate project/dll with only dependency on MakaMek.Core.
That would force correct DI direction and mitigate risk of messy code/architectural decisions.  

### 7.1 StartNewGameViewModel/JoinGameViewModel Integration

UI should allow adding bots in the lobby, reuse current AddPlayer logic, extending it to add Bots via the BotManager, not directly to the ClientGame.

### 7.2 Dependency Injection

Register bot-related services:

```csharp
// In DI configuration
services.AddTransient<IBotManager, BotManager>();
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
- [ ] Implement `IBot`, `Bot` classes
- [ ] Implement `IBotManager`, `BotManager` classes
- [ ] Integrate `BotManager` with ClientGame
- [ ] Add bot-related dependency injection configuration
- [ ] Unit tests for core bot infrastructure

### Phase 2: Basic Decision Engines 
- [ ] Implement `DeploymentDecisionEngine` with random deployment logic
- [ ] Implement `MovementDecisionEngine` with random movement logic
- [ ] Implement `WeaponsDecisionEngine` with random target selection
- [ ] Implement `EndPhaseDecisionEngine` with basic end turn logic

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
- [ ] Unit tests for each decision engine

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




