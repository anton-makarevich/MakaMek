namespace MakaMek.Tools.BotContainer.Models.Data.Mcp;

public record MovementOption(
    string MovementType, 
    double OffensiveIndex,
    double DefensiveIndex,
    int Facing
);