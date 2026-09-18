using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Client;

/// <summary>
/// Declares that a unit will not make a physical attack during the current phase.
/// </summary>
public record struct PassPhysicalAttackCommand : IClientUnitCommand
{
    /// <summary>Gets or sets the game that originated this command.</summary>
    public required Guid GameOriginId { get; set; }

    /// <summary>Gets the unit that is passing its physical-attack action.</summary>
    public required Guid UnitId { get; init; }

    /// <summary>Gets the player submitting the pass.</summary>
    public required Guid PlayerId { get; init; }

    /// <summary>Gets the command creation time.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Gets the optional idempotency key used for transport retries.</summary>
    public Guid? IdempotencyKey { get; init; }

    /// <inheritdoc />
    public string Render(ILocalizationService localizationService, IGame game)
    {
        var command = this;
        var player = game.Players.FirstOrDefault(p => p.Id == command.PlayerId);
        var unit = player?.Units.FirstOrDefault(u => u.Id == command.UnitId);
        return player == null || unit == null
            ? string.Empty
            : string.Format(localizationService.GetString("Command_PhysicalAttack_Pass"), player.Name, unit.Model);
    }
}
