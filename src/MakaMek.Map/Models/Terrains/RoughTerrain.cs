namespace Sanet.MakaMek.Map.Models.Terrains;

public class RoughTerrain : Terrain
{
    public override MakaMekTerrains Id => MakaMekTerrains.Rough;
    public override int Height => 0;
    public override int InterveningFactor => 0;

    public override int MovementCost => 1;
}
