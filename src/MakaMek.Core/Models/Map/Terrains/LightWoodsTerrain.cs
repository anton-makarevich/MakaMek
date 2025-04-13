namespace Sanet.MakaMek.Core.Models.Map.Terrains;

public class LightWoodsTerrain : Terrain
{
    public override MakaMekTerrains Id => MakaMekTerrains.LightWoods;
    public override int Height => 2;
    public override int InterveningFactor => 1;
    public override int MovementCost => 2;
}
