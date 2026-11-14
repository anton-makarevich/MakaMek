using Sanet.MakaMek.Bots.Data;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Bots.Models.Evaluation;

/// <summary>
/// Selects the weapons a unit should fire, balancing heat and ammunition.
/// </summary>
public interface IWeaponSelectionEvaluator
{
    /// <summary>
    /// Selects the weapons to fire from a weapon configuration evaluation.
    /// </summary>
    /// <param name="attacker">The unit that would fire.</param>
    /// <param name="configuration">Viable weapons and their configuration.</param>
    IReadOnlyList<WeaponEvaluationData> SelectWeapons(
        IUnit attacker,
        WeaponConfigurationEvaluationData configuration);
}
