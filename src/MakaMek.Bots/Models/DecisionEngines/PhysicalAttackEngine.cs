using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Bots.Data;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;

namespace Sanet.MakaMek.Bots.Models.DecisionEngines;

/// <summary>
/// Completes physical-attack turns for bots with a safe pass fallback until bot attack selection
/// has explicit rule-aware target and movement policies.
/// </summary>
public sealed class PhysicalAttackEngine : IBotDecisionEngine
{
    private readonly IClientGame _clientGame;

    /// <summary>Initializes the engine with the client used to submit commands.</summary>
    public PhysicalAttackEngine(IClientGame clientGame) => _clientGame = clientGame;

    /// <summary>
    /// Passes the next available unit's action so bots cannot stall the phase while unsupported
    /// physical-attack choices remain deferred.
    /// </summary>
    public async Task MakeDecision(IPlayer player, ITurnState? turnState = null, BotSettings settings = default)
    {
        try
        {
            var unit = turnState?.PhaseActiveUnitId is { } activeUnitId
                ? player.AliveUnits.FirstOrDefault(unit => unit.Id == activeUnitId)
                : player.AliveUnits.FirstOrDefault();

            if (unit == null)
                return;

            await _clientGame.PassPhysicalAttack(new PassPhysicalAttackCommand
            {
                GameOriginId = _clientGame.Id,
                PlayerId = player.Id,
                UnitId = unit.Id
            });
        }
        catch (Exception exception)
        {
            _clientGame.Logger.LogError(exception, "PhysicalAttackEngine failed for player {PlayerName}", player.Name);
        }
    }
}
