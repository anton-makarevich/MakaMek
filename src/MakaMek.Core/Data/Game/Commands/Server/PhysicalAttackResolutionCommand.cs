using System.Text;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Server;

/// <summary>Authoritative result of a resolved physical attack.</summary>
public record struct PhysicalAttackResolutionCommand : IGameCommand
{
    /// <summary>Gets or sets the authoritative game origin.</summary>
    public required Guid GameOriginId { get; set; }
    /// <summary>Gets the player who made the attack.</summary>
    public required Guid PlayerId { get; init; }
    /// <summary>Gets the attacking unit ID.</summary>
    public required Guid AttackerId { get; init; }
    /// <summary>Gets the target unit ID.</summary>
    public required Guid TargetId { get; init; }
    /// <summary>Gets the physical attack type.</summary>
    public required PhysicalAttackType AttackType { get; init; }
    /// <summary>Gets the authoritative dice and damage result.</summary>
    public required AttackResolutionData ResolutionData { get; init; }
    /// <summary>Gets the command timestamp.</summary>
    public DateTime Timestamp { get; set; }

    /// <inheritdoc />
    public string Render(ILocalizationService localizationService, IGame game)
    {
        var command = this;
        var attacker = game.Players.SelectMany(player => player.Units).FirstOrDefault(unit => unit.Id == command.AttackerId);
        var target = game.Players.SelectMany(player => player.Units).FirstOrDefault(unit => unit.Id == command.TargetId);
        if (attacker == null || target == null) return string.Empty;

        var roll = command.ResolutionData.AttackRoll.Sum(die => die.Result);
        var builder = new StringBuilder();
        builder.AppendFormat(
            localizationService.GetString("Command_PhysicalAttack_Resolution"),
            attacker.Model,
            target.Model,
            command.AttackType,
            command.ResolutionData.ToHitNumber,
            roll);

        if (command.ResolutionData.IsHit && command.ResolutionData.HitLocationsData is { } hitData)
        {
            builder.AppendLine();
            builder.AppendFormat(localizationService.GetString("Command_PhysicalAttack_Damage"), hitData.TotalDamage);
        }

        return builder.ToString();
    }
}
