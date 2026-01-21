namespace MakaMek.Tools.BotContainer.Mcp.Tools;

public record ReachableHexData(
    int Q, 
    int R, 
    List<MovementOption> Options
);