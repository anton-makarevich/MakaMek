namespace Sanet.MakaMek.Map.Models.Terrains;

/// <summary>
/// Water terrain - lakes, rivers, and other bodies of water.
/// Depth is encoded in the Height property as a negative value:
///   0  = shallow / fordable water
///  -1  = standard depth water
///  -2+ = deep water
/// </summary>
public class WaterTerrain : Terrain
{
    private readonly int _depth;

    /// <param name="depth">
    /// Depth of the water body: 0 = shallow, -1 = standard, -2 or lower = deep.
    /// Stored as a negative Height to follow the existing terrain convention.
    /// </param>
    public WaterTerrain(int depth = 0)
    {
        _depth = depth;
    }

    public override MakaMekTerrains Id => MakaMekTerrains.Water;

    /// <summary>
    /// Height is the depth value (0 = shallow, negative = deeper).
    /// </summary>
    public override int Height => _depth;

    /// <summary>
    /// Water does not block line of sight by itself.
    /// </summary>
    public override int InterveningFactor => 0;

    /// <summary>
    /// Movement cost based on water depth:
    ///   Depth 0  → 1 MP (shallow / fordable)
    ///   Depth -1 → 2 MP (standard depth)
    ///   Depth -2 or deeper → 4 MP (deep)
    /// </summary>
    public override int MovementCost => _depth switch
    {
        0 => 1,
        -1 => 2,
        _ => 4
    };
}
