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

        var locationsWithDamagedStructure = command.DamageData?.HitLocations.HitLocations
            .Where(h => h.Damage.Any(d => d.StructureDamage > 0))
            .SelectMany(h => h.Damage.Where(d => d.StructureDamage > 0))
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

        return commands;
    }
}
