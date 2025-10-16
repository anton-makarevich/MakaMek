# Bot Player System - Implementation Roadmap

**Date:** 2025-10-16  
**Status:** Ready for Implementation  
**Based on:** bot-player-system-prd.md

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
- **B) Create separate `BotViewModel` class**
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
├── IBot.cs
├── Bot.cs
├── IBotManager.cs
├── BotManager.cs
├── BotDifficulty.cs
└── DecisionEngines/
    ├── IBotDecisionEngine.cs
    ├── DeploymentEngine.cs
    ├── MovementEngine.cs
    ├── WeaponsEngine.cs
    └── EndPhaseEngine.cs

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
    public List<Guid> Bots { get; } = [];
    
    // MODIFY: Check both local and bot players
    public bool CanActivePlayerAct => ActivePlayer != null 
        && (LocalPlayers.Contains(ActivePlayer.Id) || Bots.Contains(ActivePlayer.Id))
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
            Bots.Add(player.Id);
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
    _sut.Bots.ShouldContain(player.Id);
    _sut.LocalPlayers.ShouldNotContain(player.Id);
}

[Fact]
public void CanActivePlayerAct_WhenActivePlayerIsBot_ReturnsTrue()
{
    // Arrange
    var Bot = new Player(Guid.NewGuid(), "Bot");
    _sut.JoinGameWithUnits(Bot, [], [], isBot: true);
    // Simulate server setting active player
    _sut.HandleCommand(new ChangeActivePlayerCommand 
    { 
        PlayerId = Bot.Id,
        GameOriginId = _sut.Id 
    });
    
    // Act & Assert
    _sut.CanActivePlayerAct.ShouldBeTrue();
}
```

**Acceptance Criteria:**
- [ ] `Bots` list added
- [ ] `CanActivePlayerAct` checks both lists
- [ ] `JoinGameWithUnits` has `isBot` parameter
- [ ] All tests pass

---

### Phase 1: Core Bot Infrastructure 

#### Task 1.1: Implement IBot and Bot
**Goal:** Create bot player that observes game and makes decisions

**File:** `src/MakaMek.Bots/Bot.cs`

**Implementation:**
```csharp
public class Bot : IBot
{
    private IBotDecisionEngine _decisionEngine;
    private IDisposable? _commandSubscription;
    
    // depends on current phase, stateless, the same as UIState for human players
    privte IBotDecisionEngine _currentDecisionEngine,
    
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
            // for better testability DecisionEngines can be created in BotManager and provided as collection via constructor
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

**Tests:**
```csharp
// tests/MakaMek.Bots.Tests/BotTests.cs

[Fact]
public async Task OnCommandReceived_WhenBotIsActive_MakesDecision()
{
    // Arrange
    var mockDecisionEngine = Substitute.For<IBotDecisionEngine>();
    var mockAction = Substitute.For<IBotAction>();
    mockDecisionEngine.SelectAction(Arg.Any<ClientGame>(), Arg.Any<IPlayer>())
        .Returns(mockAction);
    
    var sut = new Bot(_player, _clientGame, mockDecisionEngine, BotDifficulty.Easy);
    
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
- [ ] Bot subscribes to ClientGame.Commands
- [ ] Bot reacts to ChangePhaseCommand
- [ ] Bot reacts to ChangeActivePlayerCommand
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
    private readonly List<IBot> _bots = [];
    private readonly ClientGame? _clientGame;
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
        // Join the game with the bot's units
        _clientGame.JoinGameWithUnits(player, units, pilotAssignments); //+ a flag indicating it's a bot
        
        // Create the bot player
        var bot = new Bot(player, clientGame, difficulty);
        
        _bots.Add(bot);
    }
    
    // ... other methods
}
```

**Acceptance Criteria:**
- [ ] BotManager creates bots
- [ ] Bots are added to ClientGame.Bots
- [ ] Bots can be removed
- [ ] Cleanup works correctly
- [ ] All tests pass

---

### Phase 2: Basic Decision Engines 

#### Task 2.1: Implement Deployment Decision Engine
**Goal:** Bot can deploy units randomly

**File:** `src/MakaMek.Bots/DecisionEngines/DeploymentEngine.cs`

**Key Logic:**
1. Find undeployed units: `player.Units.Where(u => !u.IsDeployed)`
2. Get valid deployment hexes from map
3. Select random hex and direction
4. Publish `DeployUnitCommand`

**Acceptance Criteria:**
- [ ] Bot deploys all units
- [ ] Deployment positions are valid

#### Task 2.2: Implement Movement Decision Engine
**Goal:** Bot can move units randomly

**File:** `src/MakaMek.Bots/DecisionEngines/MovementEngine.cs`

**Key Logic:**
1. Find unmoved units: `player.AliveUnits.Where(u => u.MovementTypeUsed == null)`
2. Select random movement type (prefer Walk)
3. Calculate random valid path using `BattleMap.FindPath()`
4. Publish `MoveUnitCommand`

**Acceptance Criteria:**
- [ ] Bot moves all units
- [ ] Movement paths are valid
- [ ] Handles standing still

#### Task 2.3: Implement Weapons Decision Engine
**Goal:** Bot can attack targets randomly

**File:** `src/MakaMek.Bots/DecisionEngines/WeaponsEngine.cs`

**Key Logic:**
1. Find units that haven't attacked: `player.AliveUnits.Where(u => !u.HasDeclaredWeaponAttack)`
2. Find targets in range
3. Select random target
4. Select all weapons in range
5. Publish `WeaponAttackDeclarationCommand` and optionally `WeaponConfigurationCommand`

**Acceptance Criteria:**
- [ ] Bot attacks when targets available
- [ ] Bot skips when no targets
- [ ] Weapon selection is valid
- [ ] Turns torso when needed

#### Task 2.4: Implement End Phase Decision Engine
**Goal:** Bot can end turn

**File:** `src/MakaMek.Bots/DecisionEngines/EndPhaseEngine.cs`

**Key Logic:**
1. Check for shutdown units (always attempt restart)
2. Check for overheated units (shutdown if heat > 25)
3. Publish `EndTurnCommand`
4. Publish `ShutdownUnitCommand` when overheated
5. Publish `StartupUnitCommand` when shutdown

**Acceptance Criteria:**
- [ ] Bot attempts restart for shutdown units
- [ ] Bot shuts down overheated units
- [ ] Bot ends turn

---

### Phase 3: UI Integration

#### Task 3.1: Add "Add Bot" Button to Lobby
**Goal:** Users can add bots from UI

**Files to Modify:**
- files related to StartNewGame and JoinGame functionality

**Acceptance Criteria:**
- [ ] "Add Bot" button appears in lobby
- [ ] Clicking button adds bot to player list
- [ ] Bot has indicator (e.g., robot icon)
- [ ] Bot can be removed before game starts
- [ ] Existing AddPlayer infrastructure is reused as much as possible
- [ ] Bot joins game correctly

---

## Testing Strategy

### Unit Tests
- **Bot**: Activation, decision-making, error handling
- **BotManager**: Bot creation, removal, cleanup
- **Decision Engines**: Action selection for various game states
- **Bot Actions**: Command creation and execution

### Integration Tests


### Manual Testing
- **UI Flow**: Add bot, remove bot, start game
- **Performance**: Decision times, memory usage
- **User Experience**: Bot behavior feels natural
- **Bot vs Bot**: Full game completion
- **Human vs Bot**: Mixed gameplay
- **Multiple Bots**: 4 bots playing
- **Error Scenarios**: Invalid moves, exceptions, edge cases

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
4. **Implement Bot** (Phase 1, Task 1.1)
5. **Implement BotManager** (Phase 1, Task 1.2)
6. **Continue with decision engines** (Phase 2)

---

**End of Roadmap**

