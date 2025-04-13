namespace Sanet.MakaMek.Core.Models.Map.Terrains;

public class HeavyWoodsTerrain : Terrain
{
    public override MakaMekTerrains Id => MakaMekTerrains.HeavyWoods;
    public override int Height => 2;
    public override int InterveningFactor => 2;
    public override int MovementCost => 3;
}
