namespace Sanet.MakaMek.Core.Data.Game;

/// <summary>
/// Data structure containing information about a mech shutdown event
/// </summary>
public readonly record struct ShutdownData
{
    /// <summary>
    /// The reason for the shutdown
    /// </summary>
    public required ShutdownReason Reason { get; init; }
    
    /// <summary>
    /// The turn number when the shutdown occurred
    /// </summary>
    public required int Turn { get; init; }
}
