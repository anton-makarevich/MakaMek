namespace Sanet.MakaMek.Bots.Data;

/// <summary>
/// Settings that control bot behavior, particularly aggressiveness in decision-making
/// </summary>
public readonly record struct BotSettings
{
    /// <summary>
    /// Aggressiveness index (0.0 = fully defensive, 1.0 = fully aggressive)
    /// </summary>
    public float AggressivenessIndex { get; init; }

    public BotSettings(float aggressivenessIndex = 0.0f)
    {
        AggressivenessIndex = aggressivenessIndex;
    }

    /// <summary>
    /// Creates default bot settings with defensive behavior
    /// </summary>
    public static BotSettings Default => new(0.0f);
}
