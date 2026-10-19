using System.Text;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Server;

/// <summary>
/// The outcome of one physical attack with one limb, published by the server. Clients apply the
/// damage it carries as it is and never roll or recompute anything.
/// </summary>
/// <remarks>
/// A two-arm punch is published as two commands, one per arm, because each arm rolls on its own.
/// </remarks>
public record struct PhysicalAttackResolutionCommand : IGameCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid AttackerId { get; init; }
    public required Guid TargetId { get; init; }
    public required PhysicalAttackResolutionData ResolutionData { get; init; }

    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }

    public string Render(ILocalizationService localizationService, IGame game)
    {
        var command = this;
        var player = game.Players.FirstOrDefault(p => p.Id == command.PlayerId);
        var attacker = player?.Units.FirstOrDefault(u => u.Id == command.AttackerId);
        var target = game.Players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == command.TargetId);
        var targetPlayer = target?.Owner;

        if (player == null || attacker == null || target == null || targetPlayer == null)
            return string.Empty;

        // The weapon attack templates read "hits X with {2}", so the attack and limb fill that slot.
        var attackName =
            $"{ResolutionData.AttackType} ({localizationService.GetString($"MechPart_{ResolutionData.AttackerLimb}")})";
        var resolution = ResolutionData.AttackResolution;
        var rollTotal = resolution.AttackRoll.Sum(d => d.Result);
        var stringBuilder = new StringBuilder();

        stringBuilder.AppendFormat(
            localizationService.GetString(resolution.IsHit
                ? "Command_WeaponAttackResolution_Hit"
                : "Command_WeaponAttackResolution_Miss"),
            player.Name,
            attacker.Model,
            attackName,
            targetPlayer.Name,
            target.Model,
            resolution.ToHitNumber,
            rollTotal).AppendLine();

        if (!resolution.IsHit || resolution.HitLocationsData == null)
            return stringBuilder.ToString().TrimEnd();

        stringBuilder.AppendFormat(
            localizationService.GetString("Command_WeaponAttackResolution_Direction"),
            localizationService.GetString($"AttackDirection_{resolution.AttackDirection}")).AppendLine();
        stringBuilder.AppendFormat(
            localizationService.GetString("Command_WeaponAttackResolution_TotalDamage"),
            resolution.HitLocationsData.TotalDamage).AppendLine();

        if (resolution.HitLocationsData.HitLocations.Count > 0)
        {
            stringBuilder.AppendLine(localizationService.GetString("Command_WeaponAttackResolution_HitLocations"));
            foreach (var hitLocation in resolution.HitLocationsData.HitLocations)
                stringBuilder.Append(hitLocation.Render(localizationService));
        }

        if (resolution.DestroyedParts is { Count: > 0 })
        {
            stringBuilder.AppendLine(localizationService.GetString("Command_WeaponAttackResolution_DestroyedParts"));
            foreach (var location in resolution.DestroyedParts)
            {
                stringBuilder.AppendFormat(
                    localizationService.GetString("Command_WeaponAttackResolution_DestroyedPart"),
                    localizationService.GetString($"MechPart_{location}")).AppendLine();
            }
        }

        if (resolution.UnitDestroyed)
        {
            stringBuilder.AppendFormat(
                localizationService.GetString("Command_WeaponAttackResolution_UnitDestroyed"),
                target.Model).AppendLine();
        }

        return stringBuilder.ToString().TrimEnd();
    }
}
