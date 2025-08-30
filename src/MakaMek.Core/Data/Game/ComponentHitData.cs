using Sanet.MakaMek.Core.Data.Community;

namespace Sanet.MakaMek.Core.Data.Game;

public record ComponentHitData
{
    public required int Slot { get; init; }
    public required MakaMekComponent Type { get; init; }
};