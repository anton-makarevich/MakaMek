# Bot Player System Implementation Roadmap

**Date:** 2025-10-16  
**Updated:** 2025-11-16
**Status:** Ready for Implementation  
**Based on:** bot-player-system-prd.md

This document provides a concrete, actionable roadmap for implementing the bot player system in MakaMek. It distills the comprehensive analysis into specific tasks with clear acceptance criteria.

---

## Prerequisites

Before starting implementation, ensure you understand:

- **MakaMek's command pattern**: How commands flow from client → server → all clients
- **ClientGame vs ServerGame**: Client maintains local state, server is authoritative
- **Phase management**: How game phases transition and handle commands
- **UI state pattern**: How `DeploymentState`, `MovementState`, etc. work

**Recommended Reading:**
- `docs/architecture/bot-player-system-prd.md` - Original design document
- `src/MakaMek.Core/Models/Game/ClientGame.cs` - Client game implementation
- `src/MakaMek.Core/Models/Game/ServerGame.cs` - Server game implementation

---

## Critical Decisions Required

Before implementation, the following architectural decisions must be made:

### Decision 1: BotManager Lifecycle

**DECIDED:** `BotManager` is created by UI and is a singleton per client device. On game start the `BotManager` is reinitiated with a particular instance of a `ClientGame`.

### Decision 2: Bot Display in UI

**Options:**
- A) Add `IsBot` property to `PlayerViewModel`
- B) Create separate `BotViewModel` class
- C) Use naming convention (e.g., "Bot 1234")

**Recommendation:** Option A

**Rationale:**
- Minimal code changes
- Consistent with existing `IsLocalPlayer` pattern
- Easy to add bot indicator in UI
- Read `ControlType` from `IPlayer` interface

### Decision 3: Error Handling Strategy

**Options:**
- A) Fail fast (throw exceptions, stop game)
- B) Graceful degradation (log error, skip turn)
- C) Retry with fallback (retry decision, then skip)

**Recommendation:** Option B (Graceful degradation)

**Rationale:**
- Bots shouldn't break the game for human players
- Logging provides debugging information
- Skip turn is always a valid action

---

## Phase 0: Infrastructure Setup

### Task 0.1: Create MakaMek.Bots Project

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
- ✅ Project compiles
- ✅ No circular dependencies
- ✅ All interfaces defined

---

### Task 0.2: Extend IPlayer Interface with ControlType

**Goal:** Add metadata to distinguish player control mechanisms (for UI purposes only)

**File:** `src/MakaMek.Core/Models/Game/Players/IPlayer.cs`

**Changes:**

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

**Update Player Implementation:**

```csharp
public class Player : IPlayer
{
    public Guid Id { get; }
    public string Name { get; }
    public PlayerControlType ControlType { get; set; } = PlayerControlType.Local;
    
    // ... existing implementation
}
```

**Tests to Add:**

```csharp
// tests/MakaMek.Core.Tests/Models/Game/Players/PlayerTests.cs
[Fact]
public void Player_DefaultControlType_IsLocal()
{
    // Arrange & Act
    var player = new Player(Guid.NewGuid(), "Test Player");
    
    // Assert
    player.ControlType.ShouldBe(PlayerControlType.Local);
}

[Fact]
public void Player_CanSetControlType_ToBot()
{
    // Arrange
    var player = new Player(Guid.NewGuid(), "Bot Player");
    
    // Act
    player.ControlType = PlayerControlType.Bot;
    
    // Assert
    player.ControlType.ShouldBe(PlayerControlType.Bot);
}
```

**Acceptance Criteria:**
- ✅ `PlayerControlType` enum added
- ✅ `IPlayer` interface has `ControlType` property
- ✅ `Player` class implements `ControlType` with default value `Local`
- ✅ All tests pass
- ✅ No changes to `ClientGame` required

---

## Phase 1: Core Bot Implementation

### Task 1.1: Implement Bot Class

**Goal:** Create bot player that observes game and makes decisions

**File:** `src/MakaMek.Bots/Bot.cs`

**Implementation:**

```csharp
public class Bot : IBot
{
    private IBotDecisionEngine _decisionEngine;
    private IDisposable? _commandSubscription;
    
    // depends on current phase, stateless, the same as UIState for human players
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
                // for better testability DecisionEngines can be created in BotManager 
                // and provided as collection via constructor
                _currentDecisionEngine = TransferDecisionEngine(phaseCmd.Phase);
                break;
                
            case GameEndedCommand:
                Dispose();
                break;
                
            // Ignore other commands
        }
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
- ✅ Bot subscribes to ClientGame.Commands
- ✅ Bot reacts to ChangePhaseCommand
- ✅ Bot reacts to ChangeActivePlayerCommand
- ✅ Thinking delay is applied
- ✅ Decision engine is called
- ✅ Action is executed
- ✅ Error handling works
- ✅ All tests pass

---

### Task 1.2: Implement BotManager

**Goal:** Manage bot lifecycle

**File:** `src/MakaMek.Bots/BotManager.cs`

**Implementation:**

```csharp
public class BotManager : IBotManager
{
    private readonly Dictionary<Guid, IBot> _bots = new(); // Key: PlayerId
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
            
            // Optionally remove player from game
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

**Acceptance Criteria:**
- ✅ BotManager creates bots
- ✅ Bots are added to ClientGame.LocalPlayers (not a separate list)
- ✅ BotManager tracks bot instances internally
- ✅ Bots can be removed
- ✅ Cleanup works correctly
- ✅ All tests pass

---

## Phase 2: Decision Engines (Basic AI)

### Task 2.1: Deployment Decision Engine

**Goal:** Bot can deploy units randomly

**File:** `src/MakaMek.Bots/DecisionEngines/DeploymentEngine.cs`

**Key Logic:**
- Find undeployed units: `player.Units.Where(u => !u.IsDeployed)`
- Get valid deployment hexes from map
- Select random hex and direction
- Publish `DeployUnitCommand`

**Acceptance Criteria:**
- ✅ Bot deploys all units
- ✅ Deployment positions are valid

---

### Task 2.2: Movement Decision Engine

**Goal:** Bot can move units randomly

**File:** `src/MakaMek.Bots/DecisionEngines/MovementEngine.cs`

**Key Logic:**
- Find unmoved units: `player.AliveUnits.Where(u => u.MovementTypeUsed == null)`
- Select random movement type (prefer Walk)
- Calculate random valid path using `BattleMap.FindPath()`
- Publish `MoveUnitCommand`

**Acceptance Criteria:**
- ✅ Bot moves all units
- ✅ Movement paths are valid
- ✅ Handles standing still

---

### Task 2.3: Weapons Attack Decision Engine

**Goal:** Bot can attack targets randomly

**File:** `src/MakaMek.Bots/DecisionEngines/WeaponsEngine.cs`

**Key Logic:**
- Find units that haven't attacked: `player.AliveUnits.Where(u => !u.HasDeclaredWeaponAttack)`
- Find targets in range
- Select random target
- Select all weapons in range
- Publish `WeaponAttackDeclarationCommand` and optionally `WeaponConfigurationCommand`

**Acceptance Criteria:**
- ✅ Bot attacks when targets available
- ✅ Bot skips when no targets
- ✅ Weapon selection is valid
- ✅ Turns torso when needed

---

### Task 2.4: End Phase Decision Engine

**Goal:** Bot can end turn

**File:** `src/MakaMek.Bots/DecisionEngines/EndPhaseEngine.cs`

**Key Logic:**
- Check for shutdown units (always attempt restart)
- Check for overheated units (shutdown if heat > 25)
- Publish `EndTurnCommand`
- Publish `ShutdownUnitCommand` when overheated
- Publish `StartupUnitCommand` when shutdown

**Acceptance Criteria:**
- ✅ Bot attempts restart for shutdown units
- ✅ Bot shuts down overheated units
- ✅ Bot ends turn

---

## Phase 3: UI Integration

### Task 3.1: Add Bot Management to Lobby

**Goal:** Users can add bots from UI

**Files to Modify:**
- Files related to StartNewGame and JoinGame functionality

**Key Changes:**
1. Add "Add Bot" button to lobby UI
2. Create bot player with `ControlType = PlayerControlType.Bot`
3. Call `BotManager.AddBot()` instead of directly modifying `ClientGame`
4. Display bot indicator (check `player.ControlType` in UI)
5. Allow bot removal via `BotManager.RemoveBot()`

**Example ViewModel Integration:**

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
            ControlType = PlayerControlType.Bot  // This is the key
        };
        
        // BotManager handles the rest
        _botManager.AddBot(botPlayer, difficulty);
    }
}
```

**Acceptance Criteria:**
- ✅ "Add Bot" button appears in lobby
- ✅ Clicking button adds bot to player list
- ✅ Bot has indicator (e.g., robot icon) based on `ControlType`
- ✅ Bot can be removed before game starts
- ✅ Existing AddPlayer infrastructure is reused as much as possible
- ✅ Bot joins game correctly

---

## Phase 4: Testing & Validation

### Unit Tests Coverage

**Components to Test:**
- **Bot**: Activation, decision-making, error handling
- **BotManager**: Bot creation, removal, cleanup
- **Decision Engines**: Action selection for various game states
- **Bot Actions**: Command creation and execution

### Integration Tests

**Scenarios:**
- **UI Flow**: Add bot, remove bot, start game
- **Performance**: Decision times, memory usage
- **User Experience**: Bot behavior feels natural

### End-to-End Tests

**Game Scenarios:**
- **Bot vs Bot**: Full game completion
- **Human vs Bot**: Mixed gameplay
- **Multiple Bots**: 4 bots playing
- **Error Scenarios**: Invalid moves, exceptions, edge cases

---

## Success Criteria

### Functional Requirements
- ✅ Bots can join games
- ✅ Bots can deploy units
- ✅ Bots can move units
- ✅ Bots can attack targets
- ✅ Bots can end turns
- ✅ Bots can complete full games

### Performance Requirements
- ✅ Bot decisions < 3 seconds
- ✅ No UI freezing
- ✅ No memory leaks
- ✅ Network transparency maintained

### Code Quality
- ✅ All unit tests pass
- ✅ Code coverage > 80%
- ✅ No compiler warnings
- ✅ Follows existing patterns

---

## Quick Start Checklist

1. ✅ Review and approve architectural decisions (Section: Critical Decisions Required)
2. ✅ Create MakaMek.Bots project (Phase 0, Task 0.1)
3. ✅ Extend IPlayer interface (Phase 0, Task 0.2) - **No ClientGame changes needed**
4. ✅ Implement Bot (Phase 1, Task 1.1)
5. ✅ Implement BotManager (Phase 1, Task 1.2)
6. ✅ Continue with decision engines (Phase 2)

---

**End of Roadmap**