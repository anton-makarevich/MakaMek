using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Utils.TechRules;
using Sanet.MakaMek.Core.Models.Map.Factory;
using Sanet.MakaMek.Core.Utils;

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
        IMechFactory mechFactory,
        ICommandPublisher commandPublisher, 
        IDiceRoller diceRoller, 
        IToHitCalculator toHitCalculator,
        ICriticalHitsCalculator criticalHitsCalculator,
        IFallProcessor fallProcessor);

    /// <summary>
    /// Creates a new client-side game instance.
    /// </summary>
    ClientGame CreateClientGame(
        IRulesProvider rulesProvider, 
        IMechFactory mechFactory,
        ICommandPublisher commandPublisher, 
        IToHitCalculator toHitCalculator,
        IBattleMapFactory mapFactory);
}
