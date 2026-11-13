using Sanet.MakaMek.Core.Models.Game.Mechanics.Displacement;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

/// <summary>
/// Standard implementation of the phase manager that defines the default phase order
/// </summary>
public class BattleTechPhaseManager : IPhaseManager
{
    /// <summary>
    /// Gets the next phase based on the current phase
    /// </summary>
    /// <param name="currentPhase">The current phase name</param>
    /// <param name="game">The game instance</param>
    /// <returns>The next phase instance</returns>
    public IGamePhase GetNextPhase(PhaseNames currentPhase, ServerGame game)
    {
        // The phase manager is the composition root for phases: it wires the shared mechanics
        // instances into the phases and their handlers. Services must not construct their own
        // collaborators.
        var dominoEffectResolver = new DominoEffectResolver(game.DiceRoller);

        return currentPhase switch
        {
            PhaseNames.Start => new DeploymentPhase(game),
            PhaseNames.Deployment => new InitiativePhase(game),
            PhaseNames.Initiative => new MovementPhase(game, dominoEffectResolver),
            PhaseNames.Movement => new WeaponsAttackPhase(game),
            PhaseNames.WeaponsAttack => new WeaponAttackResolutionPhase(game),
            PhaseNames.WeaponAttackResolution => new PhysicalAttackPhase(game),
            PhaseNames.PhysicalAttack => new PhysicalAttackResolutionPhase(
                game,
                new PushDisplacementResolver(game.RulesProvider, dominoEffectResolver),
                new ChargeDisplacementResolver(game.RulesProvider, dominoEffectResolver),
                new DfaDisplacementResolver(game.RulesProvider, dominoEffectResolver)),
            PhaseNames.PhysicalAttackResolution => new HeatPhase(game),
            PhaseNames.Heat => new EndPhase(game),
            PhaseNames.End => new InitiativePhase(game),
            _ => new StartPhase(game)
        };
    }
}
