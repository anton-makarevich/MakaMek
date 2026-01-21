namespace MakaMek.Tools.BotContainer.Models.Data.Mcp;

public record ReachableHexData(
    int Q, 
    int R, 
    IReadOnlyList<MovementOption> Options
);