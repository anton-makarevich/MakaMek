namespace Sanet.MakaMek.Map.Models.Terrains;

/// <summary>
/// Enum representing all terrain types in MakaMek
/// </summary>
public enum MakaMekTerrains
{
    /// <summary>
    /// Clear terrain with no obstacles
    /// </summary>
    Clear = 0,
    
    /// <summary>
    /// Light woods terrain
    /// </summary>
    LightWoods = 1,
    
    /// <summary>
    /// Heavy woods terrain
    /// </summary>
    HeavyWoods = 2,
    
    /// <summary>
    /// Rough terrain - broken ground, rubble, or other difficult surface
    /// </summary>
    Rough = 3,

    /// <summary>
    /// Water terrain - lakes, rivers, or other bodies of water.
    /// Depth is stored as a non-positive Height value: 0 = shallow/fordable, -1 = standard, -2+ = deep.
    /// </summary>
    Water = 4,

    /// <summary>
    /// Road terrain - improved surface for faster travel
    /// </summary>
    Road = 5,

    /// <summary>
    /// Pavement terrain - hardened artificial surface
    /// </summary>
    Pavement = 6,

    /// <summary>
    /// Bridge terrain - elevated crossing over water or other obstacles
    /// </summary>
    Bridge = 7,

    /// <summary>
    /// Rubble terrain - debris from collapsed bridge or destroyed building
    /// </summary>
    Rubble = 8
}
