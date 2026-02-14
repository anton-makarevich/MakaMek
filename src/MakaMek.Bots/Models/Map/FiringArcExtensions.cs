using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Bots.Models.Map;

/// <summary>
/// Extension methods for FiringArc enum
/// </summary>
public static class FiringArcExtensions
{
    /// <summary>
    /// Gets the threat multiplier for a given firing arc for bot evaluation.
    /// Rear hits are weighted more heavily as they typically hit weaker armor.
    /// Side hits are also weighted higher than front hits.
    /// </summary>
    /// <param name="arc">The firing arc</param>
    /// <returns>Multiplier value for the arc (1.0 to 2.0)</returns>
    public static double GetArcMultiplier(this FiringArc arc)
    {
        return arc switch
        {
            FiringArc.Rear => 2.0,
            FiringArc.Left => 1.5,
            FiringArc.Right => 1.5,
            _ => 1.0
        };
    }
}
