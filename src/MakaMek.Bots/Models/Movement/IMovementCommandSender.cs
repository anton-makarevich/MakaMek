using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Bots.Models.Movement;

/// <summary>
/// Issues the movement-phase commands a bot decision results in.
/// </summary>
public interface IMovementCommandSender
{
    /// <summary>
    /// Attempts to stand a prone mech up. Returns <c>true</c> when a stand-up command was issued.
    /// </summary>
    Task<bool> TryStandUp(IPlayer player, IUnit unit);

    /// <summary>
    /// Moves the unit along the given path.
    /// </summary>
    Task Move(IPlayer player, IUnit unit, MovementPath path);

    /// <summary>
    /// Ends the unit's movement without moving, falling back to any other unmoved unit.
    /// </summary>
    Task SkipTurn(IPlayer player, IUnit? unit);
}
