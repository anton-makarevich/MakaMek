using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;

public interface IFallingDamageCalculator
{
    FallingDamageData CalculateFallingDamage(Unit unit, int levelsFallen, bool wasJumping);

    FallingDamageData CalculateSkidDamage(Unit unit, int skidDistance, HexDirection facingAfterFall, DiceResult facingRoll, HitDirection attackDirection);
}
