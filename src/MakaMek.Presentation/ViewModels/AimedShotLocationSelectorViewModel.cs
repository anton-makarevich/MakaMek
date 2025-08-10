using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels;

public class AimedShotLocationSelectorViewModel : BaseViewModel
{
    private readonly Unit _target;
    private readonly Action<PartLocation> _onPartSelected;
    private readonly ILocalizationService _localizationService;
    private readonly ToHitBreakdown _aimedHeadModifiersBreakdown;
    private readonly ToHitBreakdown _aimedOtherModifiersBreakdown;

    public AimedShotLocationSelectorViewModel(
        Unit target,
        ToHitBreakdown aimedHeadModifiersBreakdown,
        ToHitBreakdown aimedOtherModifiersBreakdown,
        Action<PartLocation> onPartSelected,
        ILocalizationService localizationService)
    {
        _target = target;
        
        _aimedHeadModifiersBreakdown = aimedHeadModifiersBreakdown;
        _aimedOtherModifiersBreakdown = aimedOtherModifiersBreakdown;
        _onPartSelected = onPartSelected;
        _localizationService = localizationService;
        InitializeBodyParts();
    }
    
    public UnitPartViewModel HeadPart { get; private set; } = null!;
    public UnitPartViewModel CenterTorsoPart { get; private set; } = null!;
    public UnitPartViewModel LeftTorsoPart { get; private set; } = null!;
    public UnitPartViewModel RightTorsoPart { get; private set; } = null!;
    public UnitPartViewModel LeftArmPart { get; private set; } = null!;
    public UnitPartViewModel RightArmPart { get; private set; } = null!;
    public UnitPartViewModel LeftLegPart { get; private set; } = null!;
    public UnitPartViewModel RightLegPart { get; private set; } = null!;

    private void InitializeBodyParts()
    {
        HeadPart = CreateBodyPartViewModel(PartLocation.Head);
        CenterTorsoPart = CreateBodyPartViewModel(PartLocation.CenterTorso);
        LeftTorsoPart = CreateBodyPartViewModel(PartLocation.LeftTorso);
        RightTorsoPart = CreateBodyPartViewModel(PartLocation.RightTorso);
        LeftArmPart = CreateBodyPartViewModel(PartLocation.LeftArm);
        RightArmPart = CreateBodyPartViewModel(PartLocation.RightArm);
        LeftLegPart = CreateBodyPartViewModel(PartLocation.LeftLeg);
        RightLegPart = CreateBodyPartViewModel(PartLocation.RightLeg);
    }

    private UnitPartViewModel CreateBodyPartViewModel(PartLocation location)
    {
        var part = _target.Parts.FirstOrDefault(p => p.Location == location);
        var isDestroyed = part?.IsDestroyed ?? true;
        var isSelectable = !isDestroyed;

        // Calculate hit probability for this location
        double hitProbability = 0;
        if (isSelectable)
        {
            var breakdown = location == PartLocation.Head
                ? _aimedHeadModifiersBreakdown
                : _aimedOtherModifiersBreakdown;

            if (breakdown is { HasLineOfSight: true, Total: <= 12 })
            {
                hitProbability = DiceUtils.Calculate2d6Probability(breakdown.Total);
            }
        }

        return new UnitPartViewModel
        {
            Location = location,
            IsDestroyed = isDestroyed,
            IsSelectable = isSelectable,
            HitProbability = hitProbability,
            HitProbabilityText = isSelectable ? $"{hitProbability:F0}%" : "N/A",
            CurrentArmor = part?.CurrentArmor ?? 0,
            MaxArmor = part?.MaxArmor ?? 0,
            CurrentStructure = part?.CurrentStructure ?? 0,
            MaxStructure = part?.MaxStructure ?? 0,
            Name = _localizationService.GetString($"MechPart_{location}_Short")
        };
    }

    public void SelectPart(PartLocation location)
    {
        _onPartSelected(location);
    }
}