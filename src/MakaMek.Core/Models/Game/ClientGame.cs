using Sanet.MakaMek.Core.Models.Game.Commands;
using Sanet.MakaMek.Core.Models.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Utils.TechRules;
using System.Reactive.Subjects;
using System.Reactive.Linq;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game.Combat;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Services.Transport;

namespace Sanet.MakaMek.Core.Models.Game;

public sealed class ClientGame : BaseGame
{
    private readonly Subject<IGameCommand> _commandSubject = new();
    private readonly List<IGameCommand> _commandLog = [];
    private readonly HashSet<Guid> _playersEndedTurn = [];

    public IObservable<IGameCommand> Commands => _commandSubject.AsObservable();
    public IReadOnlyList<IGameCommand> CommandLog => _commandLog;
    
    public ClientGame(IRulesProvider rulesProvider, ICommandPublisher commandPublisher, IToHitCalculator toHitCalculator)
        : base(rulesProvider, commandPublisher, toHitCalculator)
    { }

    public List<IPlayer> LocalPlayers { get; } = [];

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
            case JoinGameCommand joinGameCommand:
                OnPlayerJoined(joinGameCommand);
                break;
            case UpdatePlayerStatusCommand statusCommand:
                OnPlayerStatusUpdated(statusCommand);
                
                // If we're in the Start phase and the player who just got updated was the active player
                if (TurnPhase == PhaseNames.Start && 
                    ActivePlayer != null && 
                    statusCommand.PlayerId == ActivePlayer.Id &&
                    statusCommand.PlayerStatus == PlayerStatus.Ready)
                {
                    // Find the first local player with Joining status
                    // We need to check both that the player is in the LocalPlayers collection
                    // and that their status is Joining in the Players collection
                    ActivePlayer = Players
                        .Where(p => p.Status == PlayerStatus.Joining)
                        .FirstOrDefault(p => LocalPlayers.Any(lp => lp.Id == p.Id));
                }
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
                    ActivePlayer = LocalPlayers.FirstOrDefault();
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
                        .FirstOrDefault(p => LocalPlayers.Any(lp => lp.Id == p.Id));
                }
                break;
        }
    }

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
        if (ValidateCommand(joinCommand))
        {
            CommandPublisher.PublishCommand(joinCommand);
        }
        LocalPlayers.Add(player);
    }
    
    public void SetPlayerReady(UpdatePlayerStatusCommand readyCommand)
    {
        if (ValidateCommand(readyCommand))
        {
            CommandPublisher.PublishCommand(readyCommand);
        }
    }

    public void DeployUnit(DeployUnitCommand command)
    {
        if (ActivePlayer == null) return;
        CommandPublisher.PublishCommand(command);
    }

    public void MoveUnit(MoveUnitCommand command)
    {
        if (ActivePlayer == null) return;
        CommandPublisher.PublishCommand(command);
    }

    public void ConfigureUnitWeapons(WeaponConfigurationCommand command)
    {
        if (ActivePlayer == null) return;
        CommandPublisher.PublishCommand(command);
    }

    public void DeclareWeaponAttack(WeaponAttackDeclarationCommand command)
    {
        if (ActivePlayer == null) return;
        CommandPublisher.PublishCommand(command);
    }

    public void EndTurn(TurnEndedCommand command)
    {
        if (ActivePlayer == null) return;
        CommandPublisher.PublishCommand(command);
    }

    public override void SetBattleMap(BattleMap map)
    {
        base.SetBattleMap(map);
        TurnPhase = PhaseNames.Start;
        ActivePlayer = LocalPlayers.FirstOrDefault(p => p.Status == PlayerStatus.Joining);
    }
}