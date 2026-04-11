namespace Sanet.MakaMek.Map.Models.Terrains;

public class RoughTerrain : Terrain
{
    public override MakaMekTerrains Id => MakaMekTerrains.Rough;
    public override int Height => 0;
    public override int InterveningFactor => 0;

    /// <summary>
    /// Entering a rough terrain hex costs 1 additional MP over the base cost of 1 MP,
    /// for a total of 2 MP.
    /// </summary>
    public override int MovementCost => 2;
}
