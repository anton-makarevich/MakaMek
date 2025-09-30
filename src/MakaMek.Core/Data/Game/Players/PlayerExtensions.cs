using Sanet.MakaMek.Core.Models.Game.Players;

namespace Sanet.MakaMek.Core.Data.Game.Players;

/// <summary>
/// Extension methods for Player class
/// </summary>
public static class PlayerExtensions
{
    /// <summary>
    /// Converts a Player to PlayerData for serialization
    /// </summary>
    /// <param name="player">The player to convert</param>
    /// <returns>PlayerData representation of the player</returns>
    public static PlayerData ToData(this Player player)
    {
        return new PlayerData
        {
            Id = player.Id,
            Name = player.Name,
            Tint = player.Tint
        };
    }
}

