using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Presentation.Extensions;

/// <summary>
/// Presentation-layer extension members for Unit types
/// </summary>
public static class UnitPresentationExtensions
{
    extension(IUnit unit)
    {
        /// <summary>
        /// Gets the available walking movement points for this unit
        /// </summary>
        public int AvailableWalkingPoints => unit.GetMovementPoints(MovementType.Walk);
        
        /// <summary>
        /// Gets the available running movement points for this unit
        /// </summary>
        public int AvailableRunningPoints => unit.GetMovementPoints(MovementType.Run);
        
        /// <summary>
        /// Gets the available jumping movement points for this unit
        /// </summary>
        public int AvailableJumpingPoints => unit.GetMovementPoints(MovementType.Jump);
    }
}