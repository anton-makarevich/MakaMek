namespace Sanet.MakaMek.Map.Models;

/// <summary>
/// Represents a segment in the line of sight path.
/// When second option is present, the two coordinates represent equally valid options for the LOS path,
/// </summary>
public record LineOfSightSegment(HexCoordinates MainOption, HexCoordinates? SecondOption = null)
{
    public override int GetHashCode()
    {
        if (SecondOption == null)
            return MainOption.GetHashCode();

        // Order doesn't matter, so use XOR to get same hash for (A,B) and (B,A)
        return MainOption.GetHashCode() ^ SecondOption.GetHashCode();
    }
}