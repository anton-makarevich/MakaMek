using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;

public class ApplySkidAction(Mech mech, MechSkidCommand command) : IGameAction
{
    public IReadOnlyList<IGameCommand> Process(ServerGame game)
    {
        var commands = new List<IGameCommand>();

        game.OnMechSkidding(command);
        commands.Add(command);

        var allDamagedLocations = command.DamageData?.HitLocations.HitLocations
            .SelectMany(h => h.Damage)
            .ToList() ?? [];

        // Check for hull breach if the mech took damage while submerged
        if (allDamagedLocations.Count != 0)
        {
            var hullBreachCommand = game.HullBreachCalculator
                .CalculateAndApplyHullBreach(mech, allDamagedLocations);
            if (hullBreachCommand != null)
            {
                hullBreachCommand.GameOriginId = game.Id;
                commands.Add(hullBreachCommand);
            }
        }

        var locationsWithDamagedStructure = allDamagedLocations
            .Where(d => d.StructureDamage > 0)
            .ToList();
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

        return commands;
    }
}
