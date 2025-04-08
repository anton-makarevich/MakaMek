using Sanet.MakaMek.Core.Models.Game.Combat;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Utils.TechRules;

namespace Sanet.MakaMek.Core.Models.Game.Factories;

/// <summary>
/// Factory interface for creating game instances.
/// </summary>
public interface IGameFactory
{
    /// <summary>
    /// Creates a new server-side game instance.
    /// </summary>
    ServerGame CreateServerGame(
        IRulesProvider rulesProvider, 
        ICommandPublisher commandPublisher, 
        IDiceRoller diceRoller, 
        IToHitCalculator toHitCalculator);

    /// <summary>
    /// Creates a new client-side game instance.
    /// </summary>
    ClientGame CreateClientGame(
        IRulesProvider rulesProvider, 
        ICommandPublisher commandPublisher, 
        IToHitCalculator toHitCalculator);
}
