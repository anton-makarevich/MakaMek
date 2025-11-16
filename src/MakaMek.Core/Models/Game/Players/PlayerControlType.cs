namespace Sanet.MakaMek.Core.Models.Game.Players;

/// <summary>
/// Player metadata to indicate player type for UI purposes only
/// </summary>
public enum PlayerControlType
{
    Local,      // Human player on this client
    Bot,        // AI-controlled player on this client
    Remote      // Human player on another client
}