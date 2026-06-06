using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement;

public class MovementInterruptContext
{
    /// <summary>The full original move command</summary>
    public required MoveUnitCommand MoveCommand { get; init; }

    /// <summary>Index of the segment being evaluated</summary>
    public required int SegmentIndex { get; init; }

    /// <summary>The unit performing the move (may or may not be a Mech)</summary>
    public required IUnit Unit { get; init; }

    /// <summary>The game instance (for map lookups, FallProcessor, etc.)</summary>
    public required ServerGame Game { get; init; }
}
