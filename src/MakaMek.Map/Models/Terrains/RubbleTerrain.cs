namespace Sanet.MakaMek.Map.Models.Terrains;

public class RubbleTerrain : Terrain
{
    public override MakaMekTerrains Id => MakaMekTerrains.Rubble;
    public override int Height => 0;
    public override int InterveningFactor => 0;

    public override int MovementCost => 1;
}
