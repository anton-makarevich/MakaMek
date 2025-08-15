namespace Sanet.MakaMek.Core.Models.Units;

public enum PartLocation
{
    Head,
    CenterTorso,
    LeftTorso,
    RightTorso,
    LeftArm,
    RightArm,
    LeftLeg,
    RightLeg
}

/// <summary>
/// Extension methods for the PartLocation enum
/// </summary>
public static class PartLocationExtensions
{
    /// <summary>
    /// Determines if the part location is a leg location
    /// </summary>
    /// <param name="location">The location to check</param>
    /// <returns>True if the location is a leg, false otherwise</returns>
    public static bool IsLeg(this PartLocation location)
    {
        return location is PartLocation.LeftLeg or PartLocation.RightLeg;
    }
    
    public static bool IsArm(this PartLocation location)
    {
        return location is PartLocation.LeftArm or PartLocation.RightArm;
    }
}