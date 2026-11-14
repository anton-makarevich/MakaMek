using Sanet.MakaMek.Bots.Data;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Utils;

namespace Sanet.MakaMek.Bots.Models.Evaluation;

/// <summary>
/// Heat-aware and ammo-aware weapon selection.
/// </summary>
public sealed class WeaponSelectionEvaluator(IClientGame game) : IWeaponSelectionEvaluator
{
    // Higher values make the bot more conservative with ammo
    private const double AmmoConservationFactor = 3d;

    private const int HeatSelectionThreshold = 5;

    public IReadOnlyList<WeaponEvaluationData> SelectWeapons(
        IUnit attacker,
        WeaponConfigurationEvaluationData configuration)
    {
        var initialProjectedHeat = attacker.GetProjectedHeatValue(game.RulesProvider);
        var heatDissipation = attacker.HeatDissipation;

        var sortedWeapons = configuration.ViableWeapons
            .Where(w => w.HitProbability > 0)
            .OrderByDescending(w => w.HitProbability)
            .ThenByDescending(w => w.Weapon.Damage)
            .ToList();

        if (sortedWeapons.Count == 0)
            return [];

        var selectedWeapons = new List<WeaponEvaluationData>();
        var selectedHeat = 0;

        foreach (var evaluation in sortedWeapons)
        {
            var nextSelectedHeat = selectedHeat + evaluation.Weapon.Heat;
            if (initialProjectedHeat + nextSelectedHeat - heatDissipation > HeatSelectionThreshold)
                continue;

            if (IsFiringWeaponJustified(attacker, evaluation))
            {
                selectedWeapons.Add(evaluation);
                selectedHeat = nextSelectedHeat;
            }
        }

        return selectedWeapons;
    }

    private static bool IsFiringWeaponJustified(IUnit attacker, WeaponEvaluationData evaluation)
    {
        if (!evaluation.Weapon.RequiresAmmo)
            return true;

        var remainingShots = attacker.GetRemainingAmmoShots(evaluation.Weapon);
        if (remainingShots <= 0)
            return false;

        var requiredHitProbability = AmmoConservationFactor / (remainingShots + AmmoConservationFactor);
        requiredHitProbability = Math.Clamp(requiredHitProbability, 0d, 1d);

        return evaluation.HitProbability >= requiredHitProbability;
    }
}
