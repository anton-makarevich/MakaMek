namespace MakaMek.Tools.BotContainer.Mcp.Tools;

public record MovementOption(
    string MovementType, 
    double OffensiveIndex,
    double DefensiveIndex,
    int Facing
);