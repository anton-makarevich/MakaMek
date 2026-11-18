using Sanet.MakaMek.Core.Models.Game;

namespace Sanet.MakaMek.Bots.Models.Evaluation;

/// <summary>
/// Composition of the collaborators that <see cref="DecisionEngines.WeaponsEngine"/> delegates to.
/// </summary>
/// <param name="WeaponSelection">Selects weapons within the heat and ammo budget.</param>
public sealed record WeaponsEvaluationComponents(IWeaponSelectionEvaluator WeaponSelection)
{
    /// <summary>
    /// Wires the default weapons evaluation graph for a game. This is the single place where the
    /// weapons evaluation collaborators are constructed.
    /// </summary>
    public static WeaponsEvaluationComponents CreateDefault(IClientGame game)
        => new(new WeaponSelectionEvaluator(game));
}
