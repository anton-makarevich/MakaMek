namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons;

/// <summary>
/// Represents the rangeBracket bracket a weapon is firing at
/// </summary>
public enum RangeBracket
{
    /// <summary>
    /// Target is too close (within minimum rangeBracket)
    /// </summary>
    Minimum,
    
    /// <summary>
    /// Target is at short rangeBracket
    /// </summary>
    Short,
    
    /// <summary>
    /// Target is at medium rangeBracket
    /// </summary>
    Medium,
    
    /// <summary>
    /// Target is at long rangeBracket
    /// </summary>
    Long,
    
    /// <summary>
    /// Target is out of rangeBracket
    /// </summary>
    OutOfRange
}
