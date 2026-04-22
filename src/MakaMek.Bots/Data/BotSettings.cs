namespace Sanet.MakaMek.Bots.Data;

/// <summary>
/// Settings that control bot behavior, particularly aggressiveness in decision-making
/// </summary>
public readonly record struct BotSettings(float AggressivenessIndex = 0.0f)
{
    /// <summary>
    /// Aggressiveness index (0.0 = fully defensive, 1.0 = fully aggressive)
    /// </summary>
    public float AggressivenessIndex { get; init; } = Math.Clamp(AggressivenessIndex, 0.0f, 1.0f);

    /// <summary>
    /// Creates default bot settings with defensive behavior
    /// </summary>
    public static BotSettings Default => new(0.0f);
}
