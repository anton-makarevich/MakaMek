using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Services.Transport;
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
        IConsciousnessCalculator consciousnessCalculator,
        IHeatEffectsCalculator heatEffectsCalculator,
        IFallProcessor fallProcessor
        )
    {
        return new ServerGame(
            rulesProvider,
            mechFactory,
            commandPublisher,
            diceRoller,
            toHitCalculator,
            criticalHitsCalculator,
            pilotingSkillCalculator,
            consciousnessCalculator,
            heatEffectsCalculator,
            fallProcessor
            );
    }

    public ClientGame CreateClientGame(
        IRulesProvider rulesProvider,
        IMechFactory mechFactory,
        ICommandPublisher commandPublisher,
        IToHitCalculator toHitCalculator,
        IPilotingSkillCalculator pilotingSkillCalculator,
        IConsciousnessCalculator consciousnessCalculator,
        IHeatEffectsCalculator heatEffectsCalculator,
        IBattleMapFactory mapFactory)
    {
        return new ClientGame(
            rulesProvider,
            mechFactory,
            commandPublisher,
            toHitCalculator,
            pilotingSkillCalculator,
            consciousnessCalculator,
            heatEffectsCalculator,
            mapFactory);
    }
}
