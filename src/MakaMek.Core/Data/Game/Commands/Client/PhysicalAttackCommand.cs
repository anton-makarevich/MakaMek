using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Client;

/// <summary>Declares a physical attack against an opposing unit.</summary>
public record struct PhysicalAttackCommand : IClientUnitCommand
{
    /// <summary>Gets or sets the originating game instance.</summary>
    public required Guid GameOriginId { get; set; }
    /// <summary>Gets or sets the command creation time.</summary>
    public DateTime Timestamp { get; set; }
    /// <summary>Gets the transport retry key.</summary>
    public Guid? IdempotencyKey { get; init; }

    public string Render(ILocalizationService localizationService, IGame game)
    {
        var command = this;
        var player = game.Players.FirstOrDefault(p => p.Id == command.PlayerId);
        var unit = player?.Units.FirstOrDefault(u => u.Id == command.UnitId);
        var target = game.Players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == command.TargetUnitId);

        if (unit == null || target == null) return string.Empty;

        var localizedTemplate = localizationService.GetString("Command_PhysicalAttack");
        return string.Format(localizedTemplate,
            player?.Name,
            unit.Model,
            target.Model,
            AttackType);
    }

    /// <summary>Gets the attacking unit.</summary>
    public required Guid UnitId { get; init; }
    /// <summary>Gets the target unit.</summary>
    public required Guid TargetUnitId { get; init; }
    /// <summary>Gets the declared attack type.</summary>
    public required PhysicalAttackType AttackType { get; init; }
    /// <summary>Gets the declaring player.</summary>
    public Guid PlayerId { get; init; }
}
