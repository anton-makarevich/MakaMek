using Sanet.MakaMek.Core.Utils.TechRules;
using System.Reactive.Subjects;
using System.Reactive.Linq;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Models.Map.Factory;
using Sanet.MakaMek.Core.Utils;

namespace Sanet.MakaMek.Core.Models.Game;

public sealed class ClientGame : BaseGame
{
    private readonly Subject<IGameCommand> _commandSubject = new();
    private readonly List<IGameCommand> _commandLog = [];
    private readonly HashSet<Guid> _playersEndedTurn = [];
    private readonly IBattleMapFactory _mapFactory;

    public IObservable<IGameCommand> Commands => _commandSubject.AsObservable();
    public IReadOnlyList<IGameCommand> CommandLog => _commandLog;
    
    public ClientGame(
        IRulesProvider rulesProvider,
        IMechFactory mechFactory,
        ICommandPublisher commandPublisher,
        IToHitCalculator toHitCalculator, 
        IPilotingSkillCalculator pilotingSkillCalculator,
        IBattleMapFactory mapFactory)
        : base(rulesProvider, mechFactory, commandPublisher, toHitCalculator, pilotingSkillCalculator)
    {
        _mapFactory = mapFactory;
    }

    public List<Guid> LocalPlayers { get; } = [];

    public override void HandleCommand(IGameCommand command)
    {
        if (!ShouldHandleCommand(command)) return;
        
        // Log the command
        _commandLog.Add(command);
        
        // Publish the command to subscribers
        _commandSubject.OnNext(command);
        
        // Handle specific command types
        switch (command)
        {
            case SetBattleMapCommand setBattleMapCommand:
                // Create a new BattleMap from the received data
                var battleMap = _mapFactory.CreateFromData(setBattleMapCommand.MapData);
                SetBattleMap(battleMap);
                break;
            case JoinGameCommand joinGameCommand:
                OnPlayerJoined(joinGameCommand);
                var localPlayer = Players.FirstOrDefault(p => p.Id == joinGameCommand.PlayerId);
                if (localPlayer != null)
                {
                    localPlayer.Status = PlayerStatus.Joined;
                }
                break;
            case UpdatePlayerStatusCommand statusCommand:
                OnPlayerStatusUpdated(statusCommand);
                break;
            case TurnIncrementedCommand turnIncrementedCommand:
                // Use the validation method from BaseGame
                if (ValidateTurnIncrementedCommand(turnIncrementedCommand))
                {
                    Turn = turnIncrementedCommand.TurnNumber;
                }
                break;
            case ChangePhaseCommand phaseCommand:
                TurnPhase = phaseCommand.Phase;
                
                // When entering End phase, clear the players who ended turn and set first local player as active
                if (phaseCommand.Phase == PhaseNames.End)
                {
                    _playersEndedTurn.Clear();
                     ActivePlayer = AlivePlayers.FirstOrDefault(p =>p.Id == LocalPlayers.FirstOrDefault());
                }
                break;
            case ChangeActivePlayerCommand changeActivePlayerCommand:
                var player = Players.FirstOrDefault(p => p.Id == changeActivePlayerCommand.PlayerId);
                ActivePlayer = player;
                UnitsToPlayCurrentStep = changeActivePlayerCommand.UnitsToPlay;
                break;
            case DeployUnitCommand deployUnitCommand:
                OnDeployUnit(deployUnitCommand);
                break;
            case MoveUnitCommand moveUnitCommand:
                OnMoveUnit(moveUnitCommand);
                break;
            case WeaponConfigurationCommand weaponConfigurationCommand:
                OnWeaponConfiguration(weaponConfigurationCommand);
                break;
            case WeaponAttackDeclarationCommand weaponAttackDeclarationCommand:
                OnWeaponsAttack(weaponAttackDeclarationCommand);
                break;
            case WeaponAttackResolutionCommand attackResolutionCommand:
                OnWeaponsAttackResolution(attackResolutionCommand);
                break;
            case MechFallingCommand mechFallingCommand:
                OnMechFalling(mechFallingCommand);
                break;
            case MechStandUpCommand mechStandedUpCommand:
                OnMechStandUp(mechStandedUpCommand);
                break;
            case HeatUpdatedCommand heatUpdateCommand:
                OnHeatUpdate(heatUpdateCommand);
                break;
            case TurnEndedCommand turnEndedCommand:
                OnTurnEnded(turnEndedCommand);
                // Record that this player has ended their turn
                _playersEndedTurn.Add(turnEndedCommand.PlayerId);
                
                // If we're in the End phase and the player who just ended their turn was the active player
                if (TurnPhase == PhaseNames.End && 
                    ActivePlayer != null && 
                    turnEndedCommand.PlayerId == ActivePlayer.Id)
                {
                    // Set the next local player who hasn't ended their turn as active
                    ActivePlayer = Players
                        .Where(p => _playersEndedTurn.Contains(p.Id) == false)
                        .FirstOrDefault(p => LocalPlayers.Any(lp => lp == p.Id));
                }
                break;
        }
    }

    public bool CanActivePlayerAct => ActivePlayer != null 
                                      && LocalPlayers.Contains(ActivePlayer.Id) 
                                      && ActivePlayer.CanAct;

    public void JoinGameWithUnits(IPlayer player, List<UnitData> units)
    {
        var joinCommand = new JoinGameCommand
        {
            PlayerId = player.Id,
            PlayerName = player.Name,
            GameOriginId = Id,
            Tint = player.Tint,
            Units = units
        };
        player.Status = PlayerStatus.Joining;
        LocalPlayers.Add(player.Id);
        if (ValidateCommand(joinCommand))
        {
            CommandPublisher.PublishCommand(joinCommand);
        }
    }
    
    public void SetPlayerReady(UpdatePlayerStatusCommand readyCommand)
    {
        if (ValidateCommand(readyCommand))
        {
            readyCommand.GameOriginId = Id;
            CommandPublisher.PublishCommand(readyCommand);
        }
    }

    /// <summary>
    /// Sends a player action command if the active player can act
    /// </summary>
    /// <param name="command">Any client command to be sent</param>
    /// <typeparam name="T">Type of command that implements IClientCommand</typeparam>
    public void SendPlayerAction<T>(T command) where T : IClientCommand
    {
        if (!CanActivePlayerAct) return;
        CommandPublisher.PublishCommand(command);
    }

    public void DeployUnit(DeployUnitCommand command) => SendPlayerAction(command);
    
    public void MoveUnit(MoveUnitCommand command) => SendPlayerAction(command);
    
    public void ConfigureUnitWeapons(WeaponConfigurationCommand command) => SendPlayerAction(command);
    
    public void DeclareWeaponAttack(WeaponAttackDeclarationCommand command) => SendPlayerAction(command);
    
    public void EndTurn(TurnEndedCommand command) => SendPlayerAction(command);
    
    public void TryStandupUnit(TryStandupCommand command) => SendPlayerAction(command);

    public void RequestLobbyStatus(RequestGameLobbyStatusCommand statusCommand)
    {
        CommandPublisher.PublishCommand(statusCommand);
    }
}