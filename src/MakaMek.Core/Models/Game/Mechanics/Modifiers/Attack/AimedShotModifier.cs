using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;

/// <summary>
/// Modifier applied when making an aimed shot at a specific body part
/// </summary>
public record AimedShotModifier : RollModifier
{
    /// <summary>
    /// The specific body part being targeted
    /// </summary>
    public required PartLocation TargetLocation { get; init; }

    /// <summary>
    /// Creates an aimed shot modifier with the correct value based on the target location
    /// </summary>
    /// <param name="targetLocation">The body part being targeted</param>
    /// <returns>Aimed shot modifier with calculated value</returns>
    public static AimedShotModifier Create(PartLocation targetLocation)
    {
        // Headshots get +3 modifier, all other body parts get -4 modifier
        var modifierValue = targetLocation == PartLocation.Head ? 3 : -4;
        
        return new AimedShotModifier
        {
            TargetLocation = targetLocation,
            Value = modifierValue
        };
    }

    public override string Render(ILocalizationService localizationService)
    {
        var partKey = $"MechPart_{TargetLocation}";
        var partName = localizationService.GetString(partKey);
        
        var templateKey = TargetLocation == PartLocation.Head 
            ? "Modifier_AimedShotHead" 
            : "Modifier_AimedShotBodyPart";
            
        return string.Format(
            localizationService.GetString(templateKey), 
            partName,
            Value
        );
    }
}
