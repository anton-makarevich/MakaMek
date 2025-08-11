namespace Sanet.MakaMek.Core.Data.Game;

/// <summary>
/// Represents the reason for a mech shutdown
/// </summary>
public enum ShutdownReason
{
    /// <summary>
    /// Shutdown due to excessive heat levels
    /// </summary>
    Heat = 1,
    
    /// <summary>
    /// Voluntary shutdown by pilot
    /// </summary>
    Voluntary = 2
}