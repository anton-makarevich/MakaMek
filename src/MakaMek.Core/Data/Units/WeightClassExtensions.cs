namespace Sanet.MakaMek.Core.Data.Units;

public static class WeightClassExtensions
{
    /// <summary>
    /// Calculates the weight class based on tonnage
    /// </summary>
    public static WeightClass ToWeightClass(this int tonnage)
    {
        return tonnage switch
        {
            < 40 => WeightClass.Light,
            < 60 => WeightClass.Medium,
            < 80 => WeightClass.Heavy,
            <= 100 => WeightClass.Assault,
            _ => WeightClass.Unknown
        };
    }
}