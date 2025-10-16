# Bot Player System - Executive Summary

## Overview

This document provides a high-level summary of the bot player system design for MakaMek. For complete details, see [bot-player-system-prd.md](./bot-player-system-prd.md).

## Key Architectural Decision

**One `ClientGame` instance per bot player** (Option B)

Each bot owns its own `ClientGame` instance, mirroring the human player model. This provides:
- Clean isolation between bots
- Simple `LocalPlayers` management (one bot ID per instance)
- Easy debugging and testing
- Natural parallel processing
- Future extensibility

## Architecture Summary

```
┌─────────────────────────────────────────────────────────┐
│                    ServerGame                           │
│         (Authoritative State, Validation)               │
└────────────────┬────────────────────────────────────────┘
                 │
                 │ CommandPublisher
                 │
    ┌────────────┴──────────────────┐
    │                               │
    ▼                               ▼
RxTransportPublisher       SignalRTransportPublisher
(Local)                          (Network)
    │                               │
    ├─────────┬─────────┐           ├──────────┐
    │         │         │           │          │
    ▼         ▼         ▼           ▼          ▼
ClientGame ClientGame ClientGame  ClientGame  ClientGame
(Human)    (Bot 1)  (Bot 2)       (Remote 1) (Remote 2)
    │         │         │           │          │
    ▼         ▼         ▼           ▼          ▼
   UI      BotPlayer BotPlayer     UI         UI
          (AI Logic) (AI Logic)
```

## Core Components

### 1. BotPlayer
- Owns a `ClientGame` instance
- Observes game state via `ClientGame.Commands` and `ActivePlayerChanges`
- Delegates decision-making to `IBotDecisionEngine`
- Publishes commands through `ClientGame`

### 2. BotManager
- Creates and manages all bot players
- Integrates with `GameManager`
- Handles bot lifecycle (creation, start, stop, disposal)

### 3. Decision Engines (Phase-Specific)
- `IDeploymentDecisionEngine` - Deployment phase actions
- `IMovementDecisionEngine` - Movement phase actions
- `IWeaponsDecisionEngine` - Weapons phase actions
- `IEndPhaseDecisionEngine` - End phase actions
- `IInitiativeDecisionEngine` - Initiative rolls

### 4. Bot Actions
- `IBotAction` interface with `Execute(ClientGame)` method
- Concrete implementations: `DeployUnitAction`, `MoveUnitAction`, `DeclareWeaponAttackAction`, etc.
- Actions encapsulate command creation and publishing

## Communication Flow

1. **Server Activates Bot**: `ServerGame` → `ChangeActivePlayerCommand` → Bot's `ClientGame`
2. **Bot Observes**: `ClientGame.ActivePlayerChanges` fires
3. **Bot Decides**: `BotPlayer` → `DecisionEngine.SelectAction()` → returns `IBotAction`
4. **Bot Acts**: `IBotAction.Execute()` → publishes command via `ClientGame`
5. **Server Validates**: `ServerGame` receives and validates command
6. **Server Broadcasts**: `ServerGame` publishes result to all clients
7. **All Update**: All `ClientGame` instances (human + bot + remote) update state

## Network Transparency

Bots are **completely transparent** to remote players:
- Bots use `RxTransportPublisher` (same as local human players)
- Commands flow through the same transport layer
- Server treats bot commands identically to human commands
- Remote players see bots as regular players in the game

## Difficulty Levels

- **Easy**: Random valid actions, basic target selection
- **Medium**: Range/heat awareness, basic tactics
- **Hard**: Advanced tactics, optimal positioning, target prioritization

## Integration Points

### GameManager
```csharp
public class GameManager
{
    private IBotManager _botManager;
    
    public async Task<IBotPlayer> AddBot(string name, BotDifficulty difficulty, List<UnitData> units)
    {
        return await _botManager.AddBot(name, difficulty, units);
    }
    
    public void SetBattleMap(BattleMap map)
    {
        _serverGame?.SetBattleMap(map);
        _ = Task.Run(() => _botManager.StartAll());
    }
}
```

### StartNewGameViewModel
```csharp
public ICommand AddBotCommand => new AsyncCommand(async () =>
{
    var bot = await _gameManager.AddBot("Bot 1", BotDifficulty.Medium, units);
    // Bot automatically joins via JoinGameCommand
});
```

## Key Advantages

1. **Architectural Consistency**: Bots use the same mechanisms as human players
2. **Separation of Concerns**: Bot logic is separate from UI, server, and transport
3. **Testability**: Each component can be tested in isolation
4. **Extensibility**: Easy to add new strategies and difficulty levels
5. **Network Transparency**: Remote players can't distinguish bots from humans
6. **Maintainability**: Modular design allows easy updates

## Potential Challenges & Solutions

| Challenge | Solution |
|-----------|----------|
| Initiative rolls | Add `IInitiativeDecisionEngine` to auto-roll |
| Piloting skill rolls | Server auto-rolls for bots |
| Invalid commands | Server validation; bot retries or skips |
| Performance | Profile and optimize; async decision-making |
| Testing complexity | Mock `ClientGame`; simplified test scenarios |

## Implementation Phases

1. **Phase 1** (Week 1-2): Core infrastructure (`BotPlayer`, `BotManager`)
2. **Phase 2** (Week 3-4): Basic decision engines (random actions)
3. **Phase 3** (Week 5): UI integration (add bot button, lobby display)
4. **Phase 4** (Week 6-8): Enhanced AI (strategic decision-making)
5. **Phase 5** (Week 9-10): Testing and polish

## Success Criteria

✅ Bots complete full games without errors  
✅ Network transparency maintained  
✅ No performance degradation  
✅ Easy to add/remove bots from lobby  
✅ Extensible for new strategies  
✅ Reliable edge case handling  

## Conclusion

The bot player system design:
- ✅ Makes architectural sense given the existing codebase
- ✅ Reuses existing infrastructure (`ClientGame`, transport layer)
- ✅ Maintains separation of concerns
- ✅ Provides network transparency
- ✅ Supports future extensibility (difficulty levels, strategies, ML)
- ✅ Is testable and maintainable

**Recommendation**: Proceed with implementation using the one-ClientGame-per-bot approach.

## Related Documents

- [Full PRD](./bot-player-system-prd.md) - Complete design specification
- [Game Architecture](./Game-(Protocol)-High-Level-Architecture.md) - Current client-server architecture
- [UI States](../UiStates.md) - Human player action selection (similar pattern for bots)


