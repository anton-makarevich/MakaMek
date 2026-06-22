using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Interrupters;

internal record SkidPathResult(
    List<PathSegment> Segments,
    bool HasCliffFall,
    int LevelsFallen
);
