using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement;

public class ApplyFallAction(Mech mech, MechFallCommand command) : IGameAction
{
    public IReadOnlyList<IGameCommand> Process(ServerGame game)
    {
        var commands = new List<IGameCommand>();

        // game.OnMechFalling calls mech.ApplyFall which internally appends the Fall event to MovementTaken
        game.OnMechFalling(command);
        commands.Add(command);

        var locationsWithDamagedStructure = command.DamageData?.HitLocations.HitLocations
            .Where(h => h.Damage.Any(d => d.StructureDamage > 0))
            .SelectMany(h => h.Damage)
            .ToList() ?? [];
        if (locationsWithDamagedStructure.Count != 0)
        {
            var critCommand = game.CriticalHitsCalculator
                .CalculateAndApplyCriticalHits(mech, locationsWithDamagedStructure);
            if (critCommand != null)
            {
                critCommand.GameOriginId = game.Id;
                commands.Add(critCommand);
            }
        }

        if (mech.Pilot == null) return commands;
        var consciousnessCommands = game.ConsciousnessCalculator
            .MakeConsciousnessRolls(mech.Pilot);
        foreach (var cmd in consciousnessCommands)
        {
            var broadcastCommand = cmd;
            broadcastCommand.GameOriginId = game.Id;
            game.OnPilotConsciousnessRoll(broadcastCommand);
            commands.Add(broadcastCommand);
        }

        return commands;
    }
}
