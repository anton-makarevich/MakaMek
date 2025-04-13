namespace Sanet.MakaMek.Core.Models.Map.Terrains;

public class ClearTerrain : Terrain
{
    public override MakaMekTerrains Id => MakaMekTerrains.Clear;
    public override int Height => 0;
    public override int InterveningFactor => 0;
    public override int MovementCost => 1;
}
