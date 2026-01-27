namespace Sanet.MakaMek.Core.Data.Game.Commands;

/// <summary>
/// Error codes for command rejection
/// </summary>
public enum ErrorCode
{
    /// <summary>
    /// Command with the same idempotency key was already processed
    /// </summary>
    DuplicateCommand,
    
    /// <summary>
    /// Command validation failed
    /// </summary>
    ValidationFailed,
    
    /// <summary>
    /// Command is not allowed in the current game state
    /// </summary>
    InvalidGameState
}