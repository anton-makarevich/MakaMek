using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement;

/// <summary>
/// Maps a movement type to the physical attack it declares during the Movement Phase. A jump is
/// only ever a Death From Above and a ground move is only ever a Charge; every other movement type
/// declares no attack (Total Warfare, p. 148-149).
/// </summary>
public static class MovementTypeExtensions
{
    extension(MovementType movementType)
    {
        /// <summary>
        /// The movement-origin physical attack this movement type declares, or <c>null</c> when the
        /// movement type cannot carry one.
        /// </summary>
        public PhysicalAttackType? GetMovementAttackType() => movementType switch
        {
            MovementType.Walk or MovementType.Run => PhysicalAttackType.Charge,
            MovementType.Jump => PhysicalAttackType.DFA,
            _ => null
        };
    }
}
