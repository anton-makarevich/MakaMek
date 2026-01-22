namespace MakaMek.Tools.BotContainer.Models.Data.Mcp;

/// <summary>
/// Target option data for MCP tools
/// </summary>
public record TargetOptionData(
    Guid TargetId,
    string TargetName,
    string TargetModel,
    int TargetMass,
    int CurrentArmor,
    int MaxArmor,
    int CurrentStructure,
    int MaxStructure,
    int CurrentHeat,
    bool IsShutdown,
    IReadOnlyList<WeaponConfigurationData> Configurations
);

