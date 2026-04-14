namespace Sanet.MakaMek.Map.Models.Terrains;

/// <summary>
/// Enum representing all terrain types in MakaMek
/// </summary>
public enum MakaMekTerrains
{
    /// <summary>
    /// Clear terrain with no obstacles
    /// </summary>
    Clear,
    
    /// <summary>
    /// Light woods terrain
    /// </summary>
    LightWoods,
    
    /// <summary>
    /// Heavy woods terrain
    /// </summary>
    HeavyWoods,
    
    /// <summary>
    /// Rough terrain - broken ground, rubble, or other difficult surface
    /// </summary>
    Rough,

    /// <summary>
    /// Water terrain - lakes, rivers, or other bodies of water.
    /// Depth is stored as a non-positive Height value: 0 = shallow/fordable, -1 = standard, -2+ = deep.
    /// </summary>
    Water
}
