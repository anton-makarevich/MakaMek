using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Interrupters;

internal record SkidPathResult(
    IReadOnlyList<PathSegment> Segments,
    bool HasCliffFall,
    int LevelsFallen
);
