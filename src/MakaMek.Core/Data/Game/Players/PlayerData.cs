namespace Sanet.MakaMek.Core.Data.Game.Players;

/// <summary>
/// Data structure for player information used for serialization and caching
/// </summary>
public record struct PlayerData
{
    /// <summary>
    /// Unique identifier for the player
    /// </summary>
    public Guid Id { get; init; }
    
    /// <summary>
    /// Player's display name
    /// </summary>
    public string Name { get; init; }
    
    /// <summary>
    /// Player's color tint (hex color code)
    /// </summary>
    public string Tint { get; init; }
    
    /// <summary>
    /// Creates a default player with a random 4-digit identifier
    /// </summary>
    /// <returns>A new PlayerData with default values</returns>
    public static PlayerData CreateDefault()
    {
        var randomDigits = Random.Shared.Next(0, 10000).ToString("D4");
        
        return new PlayerData
        {
            Id = Guid.NewGuid(),
            Name = $"Player {randomDigits}",
            Tint = "#FFFFFF"
        };
    }
}

