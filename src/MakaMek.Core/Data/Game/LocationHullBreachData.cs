using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Data.Game;

public record LocationHullBreachData(
    PartLocation Location,
    bool IsAutomatic,
    int[]? BreachRoll,
    ComponentData[]? FloodedComponents,
    int EngineHitsApplied
);
