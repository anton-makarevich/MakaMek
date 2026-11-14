using Sanet.Localization;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// Builds the declaration rows of the physical attack dialog: one row per supported attack type
/// against the selected target. A row is enabled when the attack has at least one reaching limb and
/// no declaration restriction, including the displacement budget. Target numbers and breakdowns are
/// computed only for enabled rows.
/// </summary>
public sealed class PhysicalAttackOptionBuilder
{
    private static readonly IReadOnlyList<PhysicalAttackType> SupportedAttackTypes =
        PhysicalAttackDefinitions.Supported.Select(definition => definition.AttackType).ToList();

    private static readonly IReadOnlyList<PhysicalAttackOptionViewModel> NoOptions = [];

    public IReadOnlyList<PhysicalAttackOptionViewModel> Build(
        IUnit attacker,
        IUnit target,
        IClientGame game,
        IReadOnlyList<IUnit> allUnits,
        Action<PhysicalAttackOptionViewModel, bool> onSelectionChanged,
        ILocalizationService localizationService)
    {
        if (game.BattleMap is not { } map) return NoOptions;

        var options = new List<PhysicalAttackOptionViewModel>(SupportedAttackTypes.Count);
        foreach (var definition in PhysicalAttackDefinitions.Supported)
        {
            options.Add(BuildOption(definition, attacker, target, game, map, allUnits,
                onSelectionChanged, localizationService));
        }

        return options;
    }

    private PhysicalAttackOptionViewModel BuildOption(
        PhysicalAttackDefinition definition,
        IUnit attacker,
        IUnit target,
        IClientGame game,
        Sanet.MakaMek.Map.Models.IBattleMap map,
        IReadOnlyList<IUnit> allUnits,
        Action<PhysicalAttackOptionViewModel, bool> onSelectionChanged,
        ILocalizationService localizationService)
    {
        var attackType = definition.AttackType;
        var limbEligibility = game.PhysicalAttackEligibility.GetLimbEligibility(attacker, target, attackType, map);
        var restriction = game.PhysicalAttackEligibility
            .GetDeclarationRestriction(attacker, target, attackType, map, allUnits);
        var isEnabled = limbEligibility.Any(limb => limb.IsAvailable) && restriction is null;

        var restrictionReason = isEnabled ? null : GetRestrictionReason(restriction, localizationService);

        int? targetNumber = null;
        PhysicalAttackBreakdown? breakdown = null;
        if (isEnabled)
        {
            targetNumber = game.PhysicalAttackCalculator.GetToHitNumber(attacker, target, attackType, map);
            breakdown = game.PhysicalAttackCalculator.GetModifierBreakdown(attacker, target, attackType, map);
        }

        return new PhysicalAttackOptionViewModel(
            attackType,
            BuildLimbVariants(definition, limbEligibility, attacker, game, localizationService),
            isEnabled,
            restrictionReason,
            targetNumber,
            breakdown,
            onSelectionChanged,
            localizationService);
    }

    private static string GetRestrictionReason(
        PhysicalAttackRestriction? restriction,
        ILocalizationService localizationService) =>
        restriction is { } value
            ? localizationService.GetString($"PhysicalAttackRestriction_{value}")
            : localizationService.GetString("PhysicalAttackDeclaration_Unavailable");

    private static IReadOnlyList<PhysicalAttackLimbVariant> BuildLimbVariants(
        PhysicalAttackDefinition definition,
        IReadOnlyList<PhysicalAttackLimbEligibility> limbEligibility,
        IUnit attacker,
        IClientGame game,
        ILocalizationService localizationService)
    {
        if (limbEligibility.Count == 0) return [];

        var attackType = definition.AttackType;
        var variants = new List<PhysicalAttackLimbVariant>(limbEligibility.Count + 1);

        // The attack type is already the row header, so the sub-items are simply the limb names.
        // An attack that needs all of its limbs resolves as one declaration, so Push offers a
        // single "Both Arms" variant instead of one variant per arm.
        if (definition.MinLimbsPerDeclaration > 1)
        {
            if (limbEligibility.Count < definition.MinLimbsPerDeclaration) return [];

            variants.Add(CreateVariant(
                definition,
                attacker,
                game,
                localizationService.GetString("PhysicalAttack_BothArms"),
                limbEligibility.Take(definition.MinLimbsPerDeclaration).ToList()));
            return variants;
        }

        foreach (var limb in limbEligibility)
        {
            variants.Add(CreateVariant(
                definition,
                attacker,
                game,
                localizationService.GetString($"MechPart_{limb.Limb}"),
                [limb]));
        }

        if (attackType == PhysicalAttackType.Punch && limbEligibility.Count > 1)
        {
            variants.Add(CreateVariant(
                definition,
                attacker,
                game,
                localizationService.GetString("PhysicalAttack_BothArms"),
                limbEligibility.ToList()));
        }

        return variants;
    }

    /// <summary>
    /// Builds one variant from the limbs that will actually be declared. A combined variant is
    /// unavailable as soon as one of its limbs is, and its damage is the sum of the per-limb
    /// damage because a two-arm punch resolves as two independent attacks.
    /// </summary>
    private static PhysicalAttackLimbVariant CreateVariant(
        PhysicalAttackDefinition definition,
        IUnit attacker,
        IClientGame game,
        string label,
        IReadOnlyList<PhysicalAttackLimbEligibility> limbs)
    {
        var restriction = limbs
            .Select(limb => limb.Restriction)
            .FirstOrDefault(value => value is not null);
        var damage = limbs.Sum(limb =>
            game.PhysicalAttackCalculator.GetDamage(attacker, definition.AttackType, limb.Limb));

        return new PhysicalAttackLimbVariant(
            label,
            limbs.Select(limb => limb.Limb).ToList(),
            damage,
            restriction);
    }
}
