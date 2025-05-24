using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Map;

namespace Sanet.MakaMek.Core.Data.Game;

/// <summary>
/// Contains the results of a falling damage calculation
/// </summary>
public record FallingDamageData(
    int TotalDamage,
    int DamagePerGroup,
    HexDirection FacingAfterFall,
    HitLocationsData HitLocations,
    DiceResult FacingDiceRoll);
