namespace Sanet.MakaMek.Map.Models.Terrains;

public class PavementTerrain : Terrain
{
    public override MakaMekTerrains Id => MakaMekTerrains.Pavement;
    public override int Height => 0;
    public override int InterveningFactor => 0;
    public override int MovementCost => 0;
}
