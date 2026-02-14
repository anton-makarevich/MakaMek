using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Map.Exceptions;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Map.Generators;

public class AlternatingRowsGenerator : ITerrainGenerator
{
    private readonly int _width;
    private readonly int _height;
    private readonly Terrain _evenTerrain;
    private readonly Terrain _oddTerrain;

    public AlternatingRowsGenerator(int width, int height, Terrain evenTerrain, Terrain oddTerrain)
    {
        _width = width;
        _height = height;
        _evenTerrain = evenTerrain;
        _oddTerrain = oddTerrain;
    }

    public Hex Generate(HexCoordinates coordinates)
    {
        if (coordinates.Q < 1 || coordinates.Q >= _width+1 ||
            coordinates.R < 1 || coordinates.R >= _height+1)
        {
            throw new HexOutsideOfMapBoundariesException(coordinates, _width, _height);
        }
        
        return new Hex(coordinates)
            .WithTerrain(coordinates.R % 2 == 0 ? _evenTerrain : _oddTerrain);
    }
}
