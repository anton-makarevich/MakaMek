# Bot Player System - Implementation Roadmap

**Date:** 2025-10-16  
**Status:** Ready for Implementation  
**Based on:** bot-player-system-prd.md and bot-player-system-analysis.md

---

## Quick Start Guide

This document provides a concrete, actionable roadmap for implementing the bot player system in MakaMek. It distills the comprehensive analysis into specific tasks with clear acceptance criteria.

---

## Prerequisites

Before starting implementation, ensure you understand:

1. **MakaMek's command pattern**: How commands flow from client → server → all clients
2. **ClientGame vs ServerGame**: Client maintains local state, server is authoritative
3. **Phase management**: How game phases transition and handle commands
4. **UI state pattern**: How `DeploymentState`, `MovementState`, etc. work

**Recommended Reading:**
- `docs/architecture/bot-player-system-prd.md` - Original design document
- `docs/architecture/bot-player-system-analysis.md` - Detailed analysis
- `src/MakaMek.Core/Models/Game/ClientGame.cs` - Client game implementation
- `src/MakaMek.Core/Models/Game/ServerGame.cs` - Server game implementation

---

## Critical Decisions Required

Before implementation, the following architectural decisions must be made:

### Decision 1: BotManager Ownership
`BotManager` is created by UI and is a singleton per client device. On game start the BotManger is being reinitiated wit a particular instance of a ClientGame.

### Decision 2: Bot Identification in UI
**Options:**
- **A) Add `IsBot` property to `PlayerViewModel`**
- **B) Create separate `BotPlayerViewModel` class**
- **C) Use naming convention (e.g., "Bot 1234")**

**Recommendation:** Option A + C

**Rationale:**
- Minimal code changes
- Consistent with existing `IsLocalPlayer` pattern
- Easy to add bot indicator in UI

### Decision 3: Error Handling Strategy
**Options:**
- **A) Fail fast** (throw exceptions, stop game)
- **B) Graceful degradation** (log error, skip turn)
- **C) Retry with fallback** (retry decision, then skip)

**Recommendation:** Option B (Graceful degradation)

**Rationale:**
- Bots shouldn't break the game for human players
- Logging provides debugging information
- Skip turn is always a valid action

---

## Implementation Phases

### Phase 0: Foundation 

#### Task 0.1: Create Bot Project Structure
**Goal:** Set up separate project for bot logic

**Files to Create:**
```
src/MakaMek.Bots/
├── MakaMek.Bots.csproj
├── IBotPlayer.cs
├── BotPlayer.cs
├── IBotManager.cs
├── BotManager.cs
├── BotDifficulty.cs
├── DecisionEngines/
│   ├── IBotDecisionEngine.cs
│   ├── BotDecisionEngine.cs
│   ├── IDeploymentDecisionEngine.cs
│   ├── IMovementDecisionEngine.cs
│   ├── IWeaponsDecisionEngine.cs
│   └── IEndPhaseDecisionEngine.cs
└── Actions/
    └── IBotAction.cs
```

**Dependencies:**
- Reference: `MakaMek.Core` only
- No references to `MakaMek.Presentation` or `MakaMek.Avalonia`

**Acceptance Criteria:**
- [ ] Project compiles
- [ ] No circular dependencies
- [ ] All interfaces defined

#### Task 0.2: Modify ClientGame for Bot Support
**Goal:** Add bot player tracking to ClientGame

**File:** `src/MakaMek.Core/Models/Game/ClientGame.cs`

**Changes:**
```csharp
public class ClientGame : BaseGame
{
    // ADD: Bot players list
    public List<Guid> BotPlayers { get; } = [];
    
    // MODIFY: Check both local and bot players
    public bool CanActivePlayerAct => ActivePlayer != null 
        && (LocalPlayers.Contains(ActivePlayer.Id) || BotPlayers.Contains(ActivePlayer.Id))
        && ActivePlayer.CanAct;
    
    // MODIFY: Add isBot parameter
    public void JoinGameWithUnits(IPlayer player, List<UnitData> units, 
        List<PilotAssignmentData> pilotAssignments, bool isBot = false)
    {
        var joinCommand = new JoinGameCommand
        {
            PlayerId = player.Id,
            PlayerName = player.Name,
            GameOriginId = Id,
            Tint = player.Tint,
            Units = units,
            PilotAssignments = pilotAssignments
        };
        player.Status = PlayerStatus.Joining;
        
        // ADD: Route to correct list
        if (isBot)
            BotPlayers.Add(player.Id);
        else
            LocalPlayers.Add(player.Id);
            
        if (ValidateCommand(joinCommand))
        {
            CommandPublisher.PublishCommand(joinCommand);
        }
    }
}
```

**Tests to Add:**
```csharp
// tests/MakaMek.Core.Tests/Models/Game/ClientGameTests.cs

[Fact]
public void JoinGameWithUnits_WhenIsBot_AddsToBotsPlayers()
{
    // Arrange
    var player = new Player(Guid.NewGuid(), "Bot");
    
    // Act
    _sut.JoinGameWithUnits(player, [], [], isBot: true);
    
    // Assert
    _sut.BotPlayers.ShouldContain(player.Id);
    _sut.LocalPlayers.ShouldNotContain(player.Id);
}

[Fact]
public void CanActivePlayerAct_WhenActivePlayerIsBot_ReturnsTrue()
{
    // Arrange
    var botPlayer = new Player(Guid.NewGuid(), "Bot");
    _sut.JoinGameWithUnits(botPlayer, [], [], isBot: true);
    // Simulate server setting active player
    _sut.HandleCommand(new ChangeActivePlayerCommand 
    { 
        PlayerId = botPlayer.Id,
        GameOriginId = _sut.Id 
    });
    
    // Act & Assert
    _sut.CanActivePlayerAct.ShouldBeTrue();
}
```

**Acceptance Criteria:**
- [ ] `BotPlayers` list added
- [ ] `CanActivePlayerAct` checks both lists
- [ ] `JoinGameWithUnits` has `isBot` parameter
- [ ] All tests pass

---

### Phase 1: Core Bot Infrastructure (Week 2)

#### Task 1.1: Implement IBotPlayer and BotPlayer
**Goal:** Create bot player that observes game and makes decisions

**File:** `src/MakaMek.Bots/BotPlayer.cs`

**Implementation:**
```csharp
public class BotPlayer : IBotPlayer
{
    private readonly IBotDecisionEngine _decisionEngine;
    private IDisposable? _commandSubscription;
    private bool _isDisposed;
    
    public IPlayer Player { get; }
    public ClientGame ClientGame { get; }
    public BotDifficulty Difficulty { get; }
    
    public BotPlayer(
        IPlayer player,
        ClientGame clientGame,
        IBotDecisionEngine decisionEngine,
        BotDifficulty difficulty)
    {
        Player = player;
        ClientGame = clientGame;
        _decisionEngine = decisionEngine;
        Difficulty = difficulty;
        
        // Subscribe to game commands
        _commandSubscription = ClientGame.Commands.Subscribe(OnCommandReceived);
    }
    
    private async void OnCommandReceived(IGameCommand command)
    {
        try
        {
            // Only react to active player changes
            if (command is not ChangeActivePlayerCommand activePlayerCmd) return;
            
            // Check if this bot is now active
            if (activePlayerCmd.PlayerId != Player.Id) return;
            
            // Apply thinking delay
            await Task.Delay(GetThinkingDelay());
            
            // Make decision
            await MakeDecision();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Bot error: {ex.Message}");
            // Fallback: skip turn
            await SkipTurn();
        }
    }
    
    private async Task MakeDecision()
    {
        var action = await _decisionEngine.SelectAction(ClientGame, Player);
        
        if (action == null)
        {
            Console.WriteLine($"Bot {Player.Name}: No action selected, skipping");
            await SkipTurn();
            return;
        }
        
        Console.WriteLine($"Bot {Player.Name}: {action.Description}");
        action.Execute(ClientGame);
    }
    
    private Task SkipTurn()
    {
        // Phase-specific skip logic
        return Task.CompletedTask;
    }
    
    private int GetThinkingDelay()
    {
        return Difficulty switch
        {
            BotDifficulty.Easy => Random.Shared.Next(500, 1000),
            BotDifficulty.Medium => Random.Shared.Next(1000, 2000),
            BotDifficulty.Hard => Random.Shared.Next(1500, 3000),
            _ => 1000
        };
    }
    
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        
        _commandSubscription?.Dispose();
        _commandSubscription = null;
    }
}
```

**Tests:**
```csharp
// tests/MakaMek.Bots.Tests/BotPlayerTests.cs

[Fact]
public async Task OnCommandReceived_WhenBotIsActive_MakesDecision()
{
    // Arrange
    var mockDecisionEngine = Substitute.For<IBotDecisionEngine>();
    var mockAction = Substitute.For<IBotAction>();
    mockDecisionEngine.SelectAction(Arg.Any<ClientGame>(), Arg.Any<IPlayer>())
        .Returns(mockAction);
    
    var sut = new BotPlayer(_player, _clientGame, mockDecisionEngine, BotDifficulty.Easy);
    
    // Act
    _clientGame.HandleCommand(new ChangeActivePlayerCommand 
    { 
        PlayerId = _player.Id,
        GameOriginId = _clientGame.Id 
    });
    
    await Task.Delay(2000); // Wait for thinking delay
    
    // Assert
    await mockDecisionEngine.Received(1).SelectAction(_clientGame, _player);
    mockAction.Received(1).Execute(_clientGame);
}
```

**Acceptance Criteria:**
- [ ] BotPlayer subscribes to ClientGame.Commands
- [ ] BotPlayer reacts to ChangeActivePlayerCommand
- [ ] Thinking delay is applied
- [ ] Decision engine is called
- [ ] Action is executed
- [ ] Error handling works
- [ ] All tests pass

#### Task 1.2: Implement IBotManager and BotManager
**Goal:** Manage bot lifecycle

**File:** `src/MakaMek.Bots/BotManager.cs`

**Implementation:**
```csharp
public class BotManager : IBotManager
{
    private readonly List<IBotPlayer> _bots = [];
    private readonly ClientGame _clientGame;
    private readonly IBotDecisionEngineFactory _decisionEngineFactory;
    private bool _isDisposed;
    
    public IReadOnlyList<IBotPlayer> Bots => _bots;
    
    public BotManager(
        ClientGame clientGame,
        IBotDecisionEngineFactory decisionEngineFactory)
    {
        _clientGame = clientGame;
        _decisionEngineFactory = decisionEngineFactory;
    }
    
    public async Task<IBotPlayer> AddBot(
        IPlayer player, 
        List<UnitData> units,
        List<PilotAssignmentData> pilotAssignments,
        BotDifficulty difficulty)
    {
        // Join the game as a bot
        _clientGame.JoinGameWithUnits(player, units, pilotAssignments, isBot: true);
        
        // Create decision engine
        var decisionEngine = _decisionEngineFactory.Create(difficulty);
        
        // Create bot player
        var bot = new BotPlayer(player, _clientGame, decisionEngine, difficulty);
        
        _bots.Add(bot);
        
        return bot;
    }
    
    public void RemoveBot(Guid botPlayerId)
    {
        var bot = _bots.FirstOrDefault(b => b.Player.Id == botPlayerId);
        if (bot == null) return;
        
        bot.Dispose();
        _bots.Remove(bot);
        
        // Send leave command
        _clientGame.LeaveGame(botPlayerId);
    }
    
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        
        foreach (var bot in _bots)
        {
            bot.Dispose();
        }
        _bots.Clear();
    }
}
```

**Acceptance Criteria:**
- [ ] BotManager creates bots
- [ ] Bots are added to ClientGame.BotPlayers
- [ ] Bots can be removed
- [ ] Cleanup works correctly
- [ ] All tests pass

---

### Phase 2: Basic Decision Engines (Week 3-4)

#### Task 2.1: Implement Deployment Decision Engine
**Goal:** Bot can deploy units randomly

**File:** `src/MakaMek.Bots/DecisionEngines/DeploymentDecisionEngine.cs`

**Key Logic:**
1. Find undeployed units: `player.Units.Where(u => !u.IsDeployed)`
2. Get valid deployment hexes from map
3. Select random hex and direction
4. Return `DeployUnitAction`

**Acceptance Criteria:**
- [ ] Bot deploys all units
- [ ] Deployment positions are valid
- [ ] Returns null when all units deployed

#### Task 2.2: Implement Movement Decision Engine
**Goal:** Bot can move units randomly

**File:** `src/MakaMek.Bots/DecisionEngines/MovementDecisionEngine.cs`

**Key Logic:**
1. Find unmoved units: `player.AliveUnits.Where(u => u.MovementTypeUsed == null)`
2. Select random movement type (prefer Walk)
3. Calculate random valid path using `BattleMap.FindPath()`
4. Return `MoveUnitAction`

**Acceptance Criteria:**
- [ ] Bot moves all units
- [ ] Movement paths are valid
- [ ] Handles standing still
- [ ] Returns null when all units moved

#### Task 2.3: Implement Weapons Decision Engine
**Goal:** Bot can attack targets randomly

**File:** `src/MakaMek.Bots/DecisionEngines/WeaponsDecisionEngine.cs`

**Key Logic:**
1. Find units that haven't attacked: `player.AliveUnits.Where(u => !u.HasDeclaredWeaponAttack)`
2. Find targets in range
3. Select random target
4. Select all weapons in range
5. Return `DeclareWeaponAttackAction` or `SkipWeaponAttackAction`

**Acceptance Criteria:**
- [ ] Bot attacks when targets available
- [ ] Bot skips when no targets
- [ ] Weapon selection is valid
- [ ] Returns null when all units attacked

#### Task 2.4: Implement End Phase Decision Engine
**Goal:** Bot can end turn

**File:** `src/MakaMek.Bots/DecisionEngines/EndPhaseDecisionEngine.cs`

**Key Logic:**
1. Check for shutdown units (always attempt restart)
2. Check for overheated units (shutdown if heat > 25)
3. Return `EndTurnAction`

**Acceptance Criteria:**
- [ ] Bot attempts restart for shutdown units
- [ ] Bot shuts down overheated units
- [ ] Bot ends turn

---

### Phase 3: UI Integration (Week 5)

#### Task 3.1: Add "Add Bot" Button to Lobby
**Goal:** Users can add bots from UI

**Files to Modify:**
- `src/MakaMek.Presentation/ViewModels/StartNewGameViewModel.cs`
- `src/MakaMek.Avalonia/MakaMek.Avalonia/Views/StartNewGameView.axaml`

**ViewModel Changes:**
```csharp
public ICommand AddBotCommand => new AsyncCommand(AddBot);

private async Task AddBot()
{
    if (_localGame == null) return;
    
    var botPlayerData = PlayerData.CreateDefault() with 
    { 
        Name = $"Bot {Random.Shared.Next(1000, 9999)}",
        Tint = GetNextTint() 
    };
    var botPlayer = new Player(botPlayerData, Guid.NewGuid());
    
    // Get units for bot (reuse existing unit selection logic)
    var units = await SelectUnitsForBot();
    var pilotAssignments = CreatePilotAssignments(units);
    
    // Add bot via GameManager.BotManager
    await _gameManager.BotManager.AddBot(botPlayer, units, pilotAssignments, BotDifficulty.Easy);
    
    // Add to UI
    var botPlayerVm = new PlayerViewModel(
        botPlayer,
        isLocalPlayer: false,
        isBot: true,
        ...
    );
    _players.Add(botPlayerVm);
}
```

**Acceptance Criteria:**
- [ ] "Add Bot" button appears in lobby
- [ ] Clicking button adds bot to player list
- [ ] Bot has indicator (e.g., robot icon)
- [ ] Bot can be removed before game starts
- [ ] Bot joins game correctly

---

### Phase 4: Integration Testing (Week 6)

#### Task 4.1: Bot vs Bot Full Game Test
**Goal:** Verify bots can complete a full game

**Test Scenario:**
1. Create 2 bots
2. Start game
3. Let bots play until victory/defeat
4. Verify no errors or hangs

**Acceptance Criteria:**
- [ ] Game completes without errors
- [ ] All phases execute correctly
- [ ] Winner is determined
- [ ] No memory leaks

#### Task 4.2: Human vs Bot Game Test
**Goal:** Verify bots work alongside humans

**Test Scenario:**
1. Create 1 human player + 1 bot
2. Human makes moves
3. Bot responds
4. Complete game

**Acceptance Criteria:**
- [ ] Bot doesn't interfere with human turns
- [ ] Bot responds when activated
- [ ] Game feels natural

---

## Testing Strategy

### Unit Tests
- **BotPlayer**: Activation, decision-making, error handling
- **BotManager**: Bot creation, removal, cleanup
- **Decision Engines**: Action selection for various game states
- **Bot Actions**: Command creation and execution

### Integration Tests
- **Bot vs Bot**: Full game completion
- **Human vs Bot**: Mixed gameplay
- **Multiple Bots**: 4 bots playing
- **Error Scenarios**: Invalid moves, exceptions, edge cases

### Manual Testing
- **UI Flow**: Add bot, remove bot, start game
- **Performance**: Decision times, memory usage
- **User Experience**: Bot behavior feels natural

---

## Success Criteria

### Functional
- [ ] Bots can join games
- [ ] Bots can deploy units
- [ ] Bots can move units
- [ ] Bots can attack targets
- [ ] Bots can end turns
- [ ] Bots can complete full games

### Non-Functional
- [ ] Bot decisions < 3 seconds
- [ ] No UI freezing
- [ ] No memory leaks
- [ ] Network transparency maintained

### Code Quality
- [ ] All unit tests pass
- [ ] Code coverage > 80%
- [ ] No compiler warnings
- [ ] Follows existing patterns

---

## Next Steps

1. **Review and approve** architectural decisions (Section: Critical Decisions Required)
2. **Create MakaMek.Bots project** (Phase 0, Task 0.1)
3. **Modify ClientGame** (Phase 0, Task 0.2)
4. **Implement BotPlayer** (Phase 1, Task 1.1)
5. **Implement BotManager** (Phase 1, Task 1.2)
6. **Continue with decision engines** (Phase 2)

---

**End of Roadmap**

