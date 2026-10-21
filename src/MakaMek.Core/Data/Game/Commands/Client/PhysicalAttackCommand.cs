using Sanet.MakaMek.Core.Models.Game;
using Sanet.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Client;

/// <summary>
/// A unit's physical attack declaration. Every unit asked to declare sends one; a null
/// <see cref="Declaration"/> means the unit makes no physical attack this turn.
/// </summary>
public record struct PhysicalAttackCommand : IClientUnitCommand
{
    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }
    public Guid? IdempotencyKey { get; init; }

    public string Render(ILocalizationService localizationService, IGame game)
    {
        var command = this;
        var player = game.Players.FirstOrDefault(p => p.Id == command.PlayerId);
        var unit = player?.Units.FirstOrDefault(u => u.Id == command.UnitId);
        if (player == null || unit == null) return string.Empty;

        if (Declaration is not { } declaration)
        {
            var noAttackTemplate = localizationService.GetString("Command_PhysicalAttack_NoAttack");
            return string.Format(noAttackTemplate, player.Name, unit.Model);
        }

        var target = game.Players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == declaration.TargetId);
        if (target == null) return string.Empty;

        var localizedTemplate = localizationService.GetString("Command_PhysicalAttack");
        return string.Format(localizedTemplate,
            player.Name,
            unit.Model,
            target.Model,
            declaration.AttackType);
    }

    public required Guid UnitId { get; init; }

    /// <summary>
    /// The attack type, the attacking limbs and the target, or null for no attack. A punch lists
    /// one or both arms, a kick the one leg the player chose.
    /// </summary>
    public PhysicalAttackDeclarationData? Declaration { get; init; }

    public Guid PlayerId { get; init; }
}
