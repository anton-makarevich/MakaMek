using Sanet.MakaMek.Core.Models.Game.Players;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// Helper methods for UI state player action checks
/// </summary>
public static class UiStateExtensions
{
    extension(IUiState state)
    {
        /// <summary>
        /// Checks if a human player can currently act. Use this in guard clauses.
        /// Returns false if the game is null, the active player cannot act, or the active player is not human-controlled.
        /// </summary>
        public bool CanHumanPlayerAct() 
            => state.Game is { CanActivePlayerAct: true, ActivePlayer.ControlType: PlayerControlType.Human };

        /// <summary>
        /// Checks if the current active player is a local human player.
        /// This is typically used for IsActionRequired property implementations.
        /// </summary>
        public bool IsActiveHumanPlayer() 
            => state.Game?.ActivePlayer != null
               && state.Game.LocalPlayers.Contains(state.Game.ActivePlayer.Id)
               && state.Game.ActivePlayer.ControlType == PlayerControlType.Human;
    }
}
