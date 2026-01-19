using Sanet.MakaMek.Core.Data.Map;

namespace BotAgent.Models.Agents.Outputs;

/// <summary>
/// Structured output from DeploymentAgent LLM decision.
/// This record is used with MAF structured output for type-safe structured output.
/// </summary>
public record DeploymentAgentOutput
{
    /// <summary>
    /// GUID of the unit to deploy (as string for LLM output).
    /// </summary>
    public required string UnitId { get; init; }

    /// <summary>
    /// Hex position for deployment.
    /// </summary>
    public required HexCoordinateData Position { get; init; }

    /// <summary>
    /// Facing direction (0-5).
    /// 0 = Top, 1 = TopRight, 2 = BottomRight, 3 = Bottom, 4 = BottomLeft, 5 = TopLeft
    /// </summary>
    public required int Direction { get; init; }

    /// <summary>
    /// Tactical reasoning for the deployment decision.
    /// </summary>
    public required string Reasoning { get; init; }
}
