using Sanet.Localization;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// Builds the declaration rows of the physical attack dialog: one row per supported attack type
/// against the selected target. A row is enabled when the attack has at least one reaching limb and
/// no declaration restriction, including the displacement budget. Target numbers and breakdowns are
/// computed only for enabled rows.
/// </summary>
internal sealed class PhysicalAttackOptionBuilder
{
    private static readonly IReadOnlyList<PhysicalAttackOptionViewModel> NoOptions = [];

    private readonly IClientGame _game;
    private readonly ILocalizationService _localizationService;

    internal PhysicalAttackOptionBuilder(IClientGame game, ILocalizationService localizationService)
    {
        _game = game;
        _localizationService = localizationService;
    }

    public IReadOnlyList<PhysicalAttackOptionViewModel> Build(
        IUnit attacker,
        IUnit target,
        IReadOnlyList<IUnit> allUnits,
        Action<PhysicalAttackOptionViewModel, bool> onSelectionChanged)
    {
        if (_game.BattleMap is not { } map) return NoOptions;

        var options = new List<PhysicalAttackOptionViewModel>(PhysicalAttackDefinitions.Supported.Count);
        foreach (var definition in PhysicalAttackDefinitions.Supported)
        {
            options.Add(BuildOption(definition, attacker, target, map, allUnits, onSelectionChanged));
        }

        return options;
    }

    private PhysicalAttackOptionViewModel BuildOption(
        PhysicalAttackDefinition definition,
        IUnit attacker,
        IUnit target,
        IBattleMap map,
        IReadOnlyList<IUnit> allUnits,
        Action<PhysicalAttackOptionViewModel, bool> onSelectionChanged)
    {
        var attackType = definition.AttackType;
        var limbEligibility = _game.PhysicalAttackEligibility.GetLimbEligibility(attacker, target, attackType, map);
        var restriction = _game.PhysicalAttackEligibility
            .GetDeclarationRestriction(attacker, target, attackType, map, allUnits);
        var isEnabled = limbEligibility.Any(limb => limb.IsAvailable) && restriction is null;

        var restrictionReason = isEnabled ? null : GetRestrictionReason(restriction);

        int? targetNumber = null;
        PhysicalAttackBreakdown? breakdown = null;
        if (isEnabled)
        {
            targetNumber = _game.PhysicalAttackCalculator.GetToHitNumber(attacker, target, attackType, map);
            breakdown = _game.PhysicalAttackCalculator.GetModifierBreakdown(attacker, target, attackType, map);
        }

        return new PhysicalAttackOptionViewModel(
            attackType,
            BuildLimbVariants(definition, limbEligibility, attacker),
            isEnabled,
            restrictionReason,
            targetNumber,
            breakdown,
            onSelectionChanged,
            _localizationService);
    }

    private string GetRestrictionReason(PhysicalAttackRestriction? restriction) =>
        restriction is { } value
            ? _localizationService.GetString($"PhysicalAttackRestriction_{value}")
            : _localizationService.GetString("PhysicalAttackDeclaration_Unavailable");

    private List<PhysicalAttackLimbVariant> BuildLimbVariants(
        PhysicalAttackDefinition definition,
        IReadOnlyList<PhysicalAttackLimbEligibility> limbEligibility,
        IUnit attacker)
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
                _localizationService.GetString("PhysicalAttack_BothArms"),
                limbEligibility.Take(definition.MinLimbsPerDeclaration).ToList()));
            return variants;
        }

        foreach (var limb in limbEligibility)
        {
            variants.Add(CreateVariant(
                definition,
                attacker,
                _localizationService.GetString($"MechPart_{limb.Limb}"),
                [limb]));
        }

        if (attackType == PhysicalAttackType.Punch && limbEligibility.Count > 1)
        {
            variants.Add(CreateVariant(
                definition,
                attacker,
                _localizationService.GetString("PhysicalAttack_BothArms"),
                limbEligibility.ToList()));
        }

        return variants;
    }

    /// <summary>
    /// Builds one variant from the limbs that will actually be declared. A combined variant is
    /// unavailable as soon as one of its limbs is, and its damage is the sum of the per-limb
    /// damage because a two-arm punch resolves as two independent attacks.
    /// </summary>
    private PhysicalAttackLimbVariant CreateVariant(
        PhysicalAttackDefinition definition,
        IUnit attacker,
        string label,
        IReadOnlyList<PhysicalAttackLimbEligibility> limbs)
    {
        var restriction = limbs
            .Select(limb => limb.Restriction)
            .FirstOrDefault(value => value is not null);
        var damage = limbs.Sum(limb =>
            _game.PhysicalAttackCalculator.GetDamage(attacker, definition.AttackType, limb.Limb));

        return new PhysicalAttackLimbVariant(
            label,
            limbs.Select(limb => limb.Limb).ToList(),
            damage,
            restriction);
    }
}
