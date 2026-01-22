namespace MakaMek.Tools.BotContainer.Models.Data.Mcp;

/// <summary>
/// Weapon evaluation data for MCP tools
/// </summary>
public record WeaponEvaluationData(
    string WeaponName,
    int Damage,
    int Heat,
    int MinRange,
    int ShortRange,
    int MediumRange,
    int LongRange,
    string WeaponType,
    bool RequiresAmmo,
    int RemainingShots,
    double HitProbability
);

