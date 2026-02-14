using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Map.Exceptions;

public class WrongHexException : Exception
{
    public HexCoordinates Coordinates { get; }

    public WrongHexException(HexCoordinates coordinates, string message) : base(message)
    {
        Coordinates = coordinates;
    }
}
