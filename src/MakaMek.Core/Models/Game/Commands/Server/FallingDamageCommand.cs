using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Commands.Server;

/// <summary>
/// Command for applying falling damage to a unit
/// </summary>
public record struct FallingDamageCommand : IGameCommand
{
    /// <summary>
    /// The ID of the unit that fell
    /// </summary>
    public required Guid UnitId { get; init; }

    /// <summary>
    /// The number of levels the unit fell
    /// </summary>
    public int LevelsFallen { get; set; } 

    /// <summary>
    /// Whether the unit was jumping when it fell
    /// </summary>
    public bool WasJumping { get; set; } 
    
    /// <summary>
    /// The new facing of the unit after falling
    /// </summary>
    public required HexDirection NewFacing { get; init; }
    
    /// <summary>
    /// The falling damage data
    /// </summary>
    public required FallingDamageData DamageData { get; init; }

    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }
    public string Format(ILocalizationService localizationService, IGame game)
    {
        throw new NotImplementedException();
    }
}
