namespace Sanet.MakaMek.Tools.BotContainer.Models.Data.Mcp;

/// <summary>
/// Weapon configuration data for MCP tools
/// </summary>
public record WeaponConfigurationData(
    string ConfigurationType,
    int Value,
    double Score,
    IReadOnlyList<WeaponEvaluationData> ViableWeapons
);

