using Sanet.MakaMek.Core.Models.Units.Components.Engines;

namespace Sanet.MakaMek.Core.Data.Units.Components;

/// <summary>
/// State data specific to engine components
/// </summary>
public record EngineStateData(EngineType Type, int Rating) : ComponentSpecificData;