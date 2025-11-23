using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Players;

namespace Sanet.MakaMek.Core.Models.Game;

public interface IClientGame:IGame
{
    IObservable<IGameCommand> Commands { get; }
    IReadOnlyList<IGameCommand> CommandLog { get; }
    IReadOnlyList<Guid> LocalPlayers { get; }
    bool IsDisposed { get; }
    bool CanActivePlayerAct { get; }
    IPilotingSkillCalculator PilotingSkillCalculator { get; }
    IConsciousnessCalculator ConsciousnessCalculator { get; }
    IHeatEffectsCalculator HeatEffectsCalculator { get; }

    /// <summary>
    /// Returns only players with at least one alive unit (i.e., those that can act)
    /// </summary>
    IReadOnlyList<IPlayer> AlivePlayers { get; }
    
    void HandleCommand(IGameCommand command);
    Task<bool> JoinGameWithUnits(IPlayer player, List<UnitData> units, List<PilotAssignmentData> pilotAssignments);
    Task<bool> SetPlayerReady(UpdatePlayerStatusCommand readyCommand);
    Task<bool> DeployUnit(DeployUnitCommand command);
    Task<bool> MoveUnit(MoveUnitCommand command);
    Task<bool> ConfigureUnitWeapons(WeaponConfigurationCommand command);
    Task<bool> DeclareWeaponAttack(WeaponAttackDeclarationCommand command);
    Task<bool> EndTurn(TurnEndedCommand command);
    Task<bool> TryStandupUnit(TryStandupCommand command);
    Task<bool> ShutdownUnit(ShutdownUnitCommand command);
    Task<bool> StartupUnit(StartupUnitCommand command);
    void RequestLobbyStatus(RequestGameLobbyStatusCommand statusCommand);

    /// <summary>
    /// Sends a PlayerLeftCommand for the specified player
    /// </summary>
    /// <param name="playerId">The ID of the player leaving</param>
    void LeaveGame(Guid playerId);

    void Dispose();
}