namespace Sanet.MakaMek.Map.Models;

/// <summary>
/// Represents an edge of a hex with elevation difference information
/// </summary>
/// <param name="Coordinates">The hex coordinates this edge belongs to</param>
/// <param name="Direction">Which of the 6 edges (0-5, corresponding to HexDirection)</param>
/// <param name="ElevationDifference">Current hex Level minus neighbor Level (positive = current higher, negative = neighbor higher)</param>
public record HexEdge(HexCoordinates Coordinates, HexDirection Direction, int ElevationDifference);
