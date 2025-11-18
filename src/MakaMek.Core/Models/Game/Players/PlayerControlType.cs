namespace Sanet.MakaMek.Core.Models.Game.Players;

/// <summary>
/// Player metadata to indicate player type for UI purposes only
/// </summary>
public enum PlayerControlType
{
    Human,      // Human player on this client
    Bot,        // AI-controlled player on this client
    Remote      // Player on another client
}