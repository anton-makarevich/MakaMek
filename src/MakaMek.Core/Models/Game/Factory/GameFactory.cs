using Sanet.MakaMek.Core.Models.Game.Combat;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Utils.TechRules;

namespace Sanet.MakaMek.Core.Models.Game.Factory;

/// <summary>
/// Concrete implementation for creating game instances.
/// </summary>
public class GameFactory : IGameFactory
{
    public ServerGame CreateServerGame(
        IRulesProvider rulesProvider, 
        ICommandPublisher commandPublisher, 
        IDiceRoller diceRoller, 
        IToHitCalculator toHitCalculator)
    {
        return new ServerGame(rulesProvider, commandPublisher, diceRoller, toHitCalculator);
    }

    public ClientGame CreateClientGame(
        IRulesProvider rulesProvider, 
        ICommandPublisher commandPublisher, 
        IToHitCalculator toHitCalculator)
    {
        return new ClientGame(rulesProvider, commandPublisher, toHitCalculator);
    }
}
