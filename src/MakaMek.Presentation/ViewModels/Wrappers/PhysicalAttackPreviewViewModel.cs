using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Utils;
using Sanet.Localization;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

/// <summary>
/// The to-hit preview for one physical attack type against one target. It shows the target number
/// and every modifier the calculator reports, so the player sees exactly what the resolution rolls
/// against. The calculator has no limb input, so the preview is limb-independent by design.
/// </summary>
public class PhysicalAttackPreviewViewModel : BindableBase
{
    private readonly ILocalizationService _localizationService;

    public PhysicalAttackPreviewViewModel(
        IUnit attacker,
        IUnit target,
        PhysicalAttackType attackType,
        int targetNumber,
        PhysicalAttackBreakdown breakdown,
        ILocalizationService localizationService)
    {
        Attacker = attacker;
        Target = target;
        AttackType = attackType;
        TargetNumber = targetNumber;
        Breakdown = breakdown;
        _localizationService = localizationService;
    }

    public IUnit Attacker { get; }

    public IUnit Target { get; }

    public PhysicalAttackType AttackType { get; }

    public int TargetNumber { get; }

    public PhysicalAttackBreakdown Breakdown { get; }

    public string AttackTypeName => _localizationService.GetString($"PhysicalAttackType_{AttackType}");

    public string TargetName => Target.Name;

    /// <summary>
    /// A physical attack's to-hit roll is 2D6 (the 1D6 roll is only for the hit location), so a
    /// modified to-hit number of 13 or more can never succeed. A natural 2 still always hits,
    /// which the resolution roll already handles.
    /// </summary>
    public bool IsImpossible => TargetNumber >= DiceUtils.Impossible2D6Roll;

    public string TargetNumberText =>
        $"{_localizationService.GetString("Attack_TargetNumber")}: {TargetNumber}";

    public string ImpossibleText => _localizationService.GetString("Attack_ImpossibleToHit");

    /// <summary>
    /// One localized line per modifier, base modifiers first, in the calculator's fixed order.
    /// </summary>
    public IReadOnlyList<string> ModifierLines =>
        Breakdown.AllModifiers
            .Select(modifier => modifier.Render(_localizationService))
            .ToList();
}
