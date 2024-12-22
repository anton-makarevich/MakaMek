using Sanet.MekForge.Core.Exceptions;
using Sanet.MekForge.Core.Models.Map;

namespace Sanet.MekForge.Core.Utils.Generators;

public class SingleTerrainGenerator : ITerrainGenerator
{
    private readonly int _width;
    private readonly int _height;
    private readonly Terrain _terrain;

    public SingleTerrainGenerator(int width, int height, Terrain terrain)
    {
        _width = width;
        _height = height;
        _terrain = terrain;
    }

    public Hex Generate(HexCoordinates coordinates)
    {
        if (coordinates.Q < 1 || coordinates.Q >= _width+1 ||
            coordinates.R < 1 || coordinates.R >= _height+1)
        {
            throw new HexOutsideOfMapBoundariesException(coordinates, _width, _height);
        }
        
        return new Hex(coordinates).WithTerrain(_terrain);
    }
}
