namespace Sanet.MakaMek.Map.Data;

public record HexCoordinateData(int Q, int R)
{
    public override string ToString()
    {
        return $"{Q:D2}{R:D2}";
    }
};