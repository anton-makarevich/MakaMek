using Sanet.MakaMek.Map.Data;

namespace Sanet.MakaMek.Map.Models.Terrains;

public class BridgeTerrain : Terrain
{
    private readonly int _height;
    private readonly int _constructionFactor;

    public BridgeTerrain() : this(0, 0) { }

    public BridgeTerrain(int height, int constructionFactor)
    {
        _height = height;
        _constructionFactor = constructionFactor;
    }

    public override MakaMekTerrains Id => MakaMekTerrains.Bridge;
    public override int Height => _height;
    public override int InterveningFactor => 0;
    public override int MovementCost => 0;

    public int ConstructionFactor => _constructionFactor;

    public override TerrainData ToData()
    {
        return new TerrainData
        {
            Type = Id,
            Height = _height,
            ConstructionFactor = _constructionFactor
        };
    }
}
