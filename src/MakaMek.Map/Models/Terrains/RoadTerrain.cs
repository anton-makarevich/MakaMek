namespace Sanet.MakaMek.Map.Models.Terrains;

public class RoadTerrain : Terrain
{
    public override MakaMekTerrains Id => MakaMekTerrains.Road;
    public override int Height => 0;
    public override int InterveningFactor => 0;
    public override int MovementCost => 0;
}
