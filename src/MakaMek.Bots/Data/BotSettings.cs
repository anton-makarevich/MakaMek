namespace Sanet.MakaMek.Bots.Data;

/// <summary>
/// Settings that control bot behavior, particularly aggressiveness in decision-making
/// </summary>
public readonly record struct BotSettings
{
    /// <summary>
    /// Aggressiveness index (0.0 = fully defensive, 1.0 = fully aggressive)
    /// </summary>
    public required float AggressivenessIndex { get; init; }

    /// <summary>
    /// Creates default bot settings with defensive behavior
    /// </summary>
    public static BotSettings Default => new() { AggressivenessIndex = 0.0f };
}
