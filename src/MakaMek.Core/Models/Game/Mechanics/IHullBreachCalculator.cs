using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

public interface IHullBreachCalculator
{
    HullBreachCommand? CalculateAndApplyHullBreach(IUnit unit, List<LocationDamageData> damagedLocations);
}
