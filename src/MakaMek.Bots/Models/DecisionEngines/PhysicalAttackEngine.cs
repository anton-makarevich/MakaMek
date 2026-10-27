using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Bots.Data;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;

namespace Sanet.MakaMek.Bots.Models.DecisionEngines;

/// <summary>
/// Decision engine for the physical attack declaration phase.
/// <para>
/// Physical attack tactics are not implemented yet (M2/M3), so every unit the server expects to
/// declare sends a null declaration, which means "no attack". The server only advances the turn
/// order when it receives a <see cref="PhysicalAttackCommand"/>, so without this the phase would
/// stall forever.
/// </para>
/// </summary>
public class PhysicalAttackEngine : IBotDecisionEngine
{
    private readonly IClientGame _clientGame;

    public PhysicalAttackEngine(IClientGame clientGame)
    {
        _clientGame = clientGame;
    }

    public async Task MakeDecision(IPlayer player, ITurnState? turnState = null, BotSettings settings = default)
    {
        if (_clientGame.TurnPhase != PhaseNames.PhysicalAttack) return;

        var map = _clientGame.BattleMap;
        if (map == null) return;

        var allUnits = _clientGame.Players.SelectMany(p => p.Units).ToList();
        var pendingUnits = player.AliveUnits
            .Where(unit => !unit.HasDeclaredPhysicalAttack
                           && _clientGame.PhysicalAttackEligibility.HasAnyOption(unit, allUnits, map))
            .ToList();

        foreach (var unit in pendingUnits)
        {
            var command = new PhysicalAttackCommand
            {
                GameOriginId = _clientGame.Id,
                PlayerId = player.Id,
                UnitId = unit.Id,
                Declaration = null
            };

            var accepted = await _clientGame.DeclarePhysicalAttack(command);
            if (accepted) continue;

            _clientGame.Logger.LogWarning(
                "Bot {PlayerName} could not declare the end of the physical attack phase for unit {UnitId}",
                player.Name, unit.Id);
            return;
        }
    }
}
