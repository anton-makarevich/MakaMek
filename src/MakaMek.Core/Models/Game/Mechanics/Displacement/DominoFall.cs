using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Displacement;

/// <summary>
/// An automatic fall a domino chain causes when it displaces a unit into a hex two or more levels
/// lower (Total Warfare p. 151-152). The fall is applied after the displacement, with normal
/// falling damage based on <see cref="LevelsFallen"/>.
/// </summary>
public sealed record DominoFall(IUnit Unit, int LevelsFallen);
