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
    /// <param name="location">The location to check</param>
    extension(PartLocation location)
    {
        /// <summary>
        /// Determines if the part location is a leg location
        /// </summary>
        /// <returns>True if the location is a leg, false otherwise</returns>
        public bool IsLeg()
        {
            return location is PartLocation.LeftLeg or PartLocation.RightLeg;
        }

        public bool IsArm()
        {
            return location is PartLocation.LeftArm or PartLocation.RightArm;
        }
        
        public bool IsTorso()
        {
            return location is PartLocation.CenterTorso
                or PartLocation.LeftTorso 
                or PartLocation.RightTorso;
        }
    }
}