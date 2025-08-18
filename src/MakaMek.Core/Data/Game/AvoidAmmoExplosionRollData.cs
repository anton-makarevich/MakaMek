namespace Sanet.MakaMek.Core.Data.Game;

/// <summary>
/// Data structure containing information about an ammo explosion avoidance roll
/// </summary>
public record AvoidAmmoExplosionRollData
{
    /// <summary>
    /// The heat level that triggered the ammo explosion check
    /// </summary>
    public int HeatLevel { get; init; }

    /// <summary>
    /// The dice results for the ammo explosion avoidance roll
    /// </summary>
    public int[] DiceResults { get; init; } = [];
    
    /// <summary>
    /// The target number that needed to be met to avoid explosion
    /// </summary>
    public int AvoidNumber { get; init; }
    
    /// <summary>
    /// Whether the roll was successful (explosion avoided)
    /// </summary>
    public required bool IsSuccessful { get; init; }
}
