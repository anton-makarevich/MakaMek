using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Utils;

namespace Sanet.MakaMek.Core.Models.Game;

public class ServerGame : BaseGame, IDisposable
{
    private IGamePhase _currentPhase;
    private List<IPlayer> _initiativeOrder = [];
    private bool _isDisposed;

    public bool IsAutoRoll { get; set; } = true;
    private IPhaseManager PhaseManager { get; }
    public IDiceRoller DiceRoller { get; }

    public ServerGame(
        IRulesProvider rulesProvider,
        IMechFactory mechFactory,
        ICommandPublisher commandPublisher,
        IDiceRoller diceRoller,
        IToHitCalculator toHitCalculator,
        IDamageTransferCalculator damageTransferCalculator,
        ICriticalHitsCalculator criticalHitsCalculator,
        IPilotingSkillCalculator pilotingSkillCalculator,
        IConsciousnessCalculator consciousnessCalculator,
        IHeatEffectsCalculator heatEffectsCalculator,
        IFallProcessor fallProcessor,
        IPhaseManager? phaseManager = null)
        : base(rulesProvider, mechFactory, commandPublisher, toHitCalculator, pilotingSkillCalculator, consciousnessCalculator, heatEffectsCalculator)
    {
        DiceRoller = diceRoller;
        DamageTransferCalculator = damageTransferCalculator;
        CriticalHitsCalculator = criticalHitsCalculator;
        FallProcessor = fallProcessor;
        PhaseManager = phaseManager ?? new BattleTechPhaseManager();
        _currentPhase = new StartPhase(this); // Starts in the StartPhase
    }
    
    public override bool IsDisposed => _isDisposed;

    public IDamageTransferCalculator DamageTransferCalculator { get; }

    public ICriticalHitsCalculator CriticalHitsCalculator { get; }

    public IFallProcessor FallProcessor { get; }

    public IReadOnlyList<IPlayer> InitiativeOrder => _initiativeOrder;
    
    public bool IsGameOver { get; private set; }

    public override void SetBattleMap(BattleMap map)
    {
        if (TurnPhase!= PhaseNames.Start) return; // Prevent changing map mid-game
        BattleMap = map;
        
        // Create and publish a command to send the map to all clients
        var mapCommand = new SetBattleMapCommand
        {
            GameOriginId = Id,
            MapData = map.ToData()
        };
        
        CommandPublisher.PublishCommand(mapCommand);
        
        ((StartPhase)_currentPhase).TryTransitionToNextPhase();
    }
    
    public void SetInitiativeOrder(IReadOnlyList<IPlayer> order)
    {
        _initiativeOrder = order.ToList();
    }

    private void TransitionToPhase(IGamePhase newPhase)
    {
        if (_currentPhase is GamePhase currentGamePhase)
        {
            currentGamePhase.Exit();
        }
        _currentPhase = newPhase;
        SetPhase(newPhase.Name);
        _currentPhase.Enter();
    }

    public void TransitionToNextPhase(PhaseNames currentPhase)
    {
        var nextPhase = PhaseManager.GetNextPhase(currentPhase, this);
        TransitionToPhase(nextPhase);
    }

    public override void HandleCommand(IGameCommand command)
    {
        if (command is not IClientCommand and not RequestGameLobbyStatusCommand) return;
        if (!ShouldHandleCommand(command)) return;
        if (!ValidateCommand(command)) return;

        // Handle PlayerLeftCommand before delegating to phase
        if (command is PlayerLeftCommand playerLeftCommand)
        {
            OnPlayerLeft(playerLeftCommand);
            return;
        }

        _currentPhase.HandleCommand(command);
    }

    protected override void OnPlayerLeft(PlayerLeftCommand command)
    {
        base.OnPlayerLeft(command);
        // At the moment it's not possible to continue even if one player is leaving
        StopGame(GameEndReason.PlayersLeft);
    }

    /// <summary>
    /// Stops the game and notifies all clients
    /// </summary>
    public void StopGame(GameEndReason reason)
    {
        if (_isDisposed || IsGameOver) return;

        // Publish GameEndedCommand to all clients
        var endCommand = new GameEndedCommand
        {
            GameOriginId = Id,
            Reason = reason,
            Timestamp = DateTime.UtcNow
        };
        CommandPublisher.PublishCommand(endCommand);

        // Mark game as over (will exit Start() loop)
        IsGameOver = true;
    }

    public void SetActivePlayer(IPlayer? player, int unitsToMove)
    {
        ActivePlayer = player;
        UnitsToPlayCurrentStep = unitsToMove;
        if (player != null)
        {
            CommandPublisher.PublishCommand(new ChangeActivePlayerCommand
            {
                GameOriginId = Id,
                PlayerId = player.Id,
                UnitsToPlay = unitsToMove
            });
        }
    }

    public void SetPhase(PhaseNames phase)
    {
        TurnPhase = phase;
        CommandPublisher.PublishCommand(new ChangePhaseCommand
        {
            GameOriginId = Id,
            Phase = phase
        });
    }

    public void IncrementTurn()
    {
        Turn++;
        _initiativeOrder.Clear(); // Clear initiative order at the start of new turn

        // Send a turn increment command to all clients
        CommandPublisher.PublishCommand(new TurnIncrementedCommand
        {
            GameOriginId = Id,
            TurnNumber = Turn
        });
    }

    public async Task Start()
    {
        // The game loop is driven by commands and phase transitions
        // This method mainly keeps the server alive until disposed
        while (!_isDisposed && !IsGameOver)
        {
            await Task.Delay(100); // Keep the task alive but idle
        }
    }
    
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        IsGameOver = true; // Ensure the loop in Start() exits
        CommandPublisher.Unsubscribe(HandleCommand);
        // Add any specific cleanup for ServerGame if needed (e.g., unsubscribe from events)
        GC.SuppressFinalize(this);
    }
}