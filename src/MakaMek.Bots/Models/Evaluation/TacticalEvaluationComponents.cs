using Sanet.MakaMek.Core.Models.Game;

namespace Sanet.MakaMek.Bots.Models.Evaluation;

/// <summary>
/// Composition of the collaborators that <see cref="DecisionEngines.TacticalEvaluator"/> delegates to.
/// </summary>
/// <param name="Snapshots">Projects unit state into immutable evaluation snapshots.</param>
/// <param name="DefensiveValue">Scores the defensive value of a position.</param>
/// <param name="OffensiveValue">Scores weapon configurations against a target.</param>
public sealed record TacticalEvaluationComponents(
    IUnitSnapshotFactory Snapshots,
    IDefensiveValueEvaluator DefensiveValue,
    IOffensiveValueEvaluator OffensiveValue)
{
    /// <summary>
    /// Wires the default evaluation graph for a game. This is the single place where the
    /// evaluation collaborators are constructed.
    /// </summary>
    public static TacticalEvaluationComponents CreateDefault(IClientGame game)
    {
        var lineOfSight = new BattleMapLineOfSightEvaluator(game);
        var hitProbability = new HitProbabilityCalculator(game);
        var weaponThreats = new WeaponThreatEvaluator(lineOfSight, hitProbability);

        return new TacticalEvaluationComponents(
            new UnitSnapshotFactory(),
            new DefensiveValueEvaluator(weaponThreats),
            new OffensiveValueEvaluator(lineOfSight, hitProbability));
    }
}
