using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Utils.TechRules;
using Sanet.MakaMek.Core.Models.Map.Factory;
using Sanet.MakaMek.Core.Utils;

namespace Sanet.MakaMek.Core.Models.Game.Factories;

/// <summary>
/// Concrete implementation for creating game instances.
/// </summary>
public class GameFactory : IGameFactory
{
    public ServerGame CreateServerGame(
        IRulesProvider rulesProvider, 
        IMechFactory mechFactory,
        ICommandPublisher commandPublisher, 
        IDiceRoller diceRoller, 
        IToHitCalculator toHitCalculator,
        ICriticalHitsCalculator criticalHitsCalculator,
        IPilotingSkillCalculator pilotingSkillCalculator,
        IFallProcessor fallProcessor)
    {
        return new ServerGame(
            rulesProvider, 
            mechFactory, 
            commandPublisher, 
            diceRoller, 
            toHitCalculator, 
            criticalHitsCalculator,
            pilotingSkillCalculator,
            fallProcessor
            );
    }

    public ClientGame CreateClientGame(
        IRulesProvider rulesProvider, 
        IMechFactory mechFactory,
        ICommandPublisher commandPublisher, 
        IToHitCalculator toHitCalculator,
        IPilotingSkillCalculator pilotingSkillCalculator,
        IBattleMapFactory mapFactory)
    {
        return new ClientGame(
            rulesProvider,
            mechFactory,
            commandPublisher,
            toHitCalculator, 
            pilotingSkillCalculator,
            mapFactory);
    }
}
