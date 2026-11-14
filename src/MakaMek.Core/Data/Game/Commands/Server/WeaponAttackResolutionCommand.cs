using System.Text;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Server;

/// <summary>
/// The outcome of one weapon attack against one target, published by the server. Clients apply the
/// damage it carries as it is and never roll or recompute anything.
/// </summary>
/// <remarks>
/// Cluster hits of one attack are carried inside this single command. A game applies the effects of
/// a resolution once, however many times the command is delivered.
/// </remarks>
public record struct WeaponAttackResolutionCommand : IGameCommand
{
    /// <summary>
    /// Identifies this one resolution. A game applies the effects of a resolution once, however many
    /// times the command is delivered.
    /// </summary>
    public required Guid ResolutionId { get; init; }

    public required Guid PlayerId { get; init; }
    public required Guid AttackerId { get; init; }
    public required ComponentData WeaponData { get; init; }
    public required Guid TargetId { get; init; }
    public required AttackResolutionData ResolutionData { get; init; }

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
        {
            return string.Empty;
        }

        var stringBuilder = new StringBuilder();
        if (ResolutionData.IsHit)
            AppendHit(stringBuilder, localizationService, player, attacker, target, targetPlayer);
        else
            AppendMiss(stringBuilder, localizationService, player, attacker, target, targetPlayer);

        return stringBuilder.ToString().TrimEnd();
    }

    private void AppendHit(
        StringBuilder stringBuilder,
        ILocalizationService localizationService,
        IPlayer player,
        IUnit attacker,
        IUnit target,
        IPlayer targetPlayer)
    {
        AppendAttackRoll(stringBuilder, localizationService, player, attacker, target, targetPlayer,
            "Command_WeaponAttackResolution_Hit");

        stringBuilder.AppendFormat(
            localizationService.GetString("Command_WeaponAttackResolution_Direction"),
            localizationService.GetString($"AttackDirection_{ResolutionData.AttackDirection}")).AppendLine();

        if (ResolutionData.HitLocationsData is not { } hitLocationsData) return;

        AppendDamageSummary(stringBuilder, localizationService, hitLocationsData);
        AppendClusterRoll(stringBuilder, localizationService, hitLocationsData);
        AppendHitLocations(stringBuilder, localizationService, hitLocationsData);

        if (ResolutionData.DestroyedParts is { Count: > 0 } destroyedParts)
            AppendDestroyedParts(stringBuilder, localizationService, destroyedParts);

        if (ResolutionData.UnitDestroyed)
        {
            stringBuilder.AppendFormat(
                localizationService.GetString("Command_WeaponAttackResolution_UnitDestroyed"),
                target.Model).AppendLine();
        }
    }

    private void AppendMiss(
        StringBuilder stringBuilder,
        ILocalizationService localizationService,
        IPlayer player,
        IUnit attacker,
        IUnit target,
        IPlayer targetPlayer) =>
        AppendAttackRoll(stringBuilder, localizationService, player, attacker, target, targetPlayer,
            "Command_WeaponAttackResolution_Miss");

    private void AppendAttackRoll(
        StringBuilder stringBuilder,
        ILocalizationService localizationService,
        IPlayer player,
        IUnit attacker,
        IUnit target,
        IPlayer targetPlayer,
        string templateKey)
    {
        var rollTotal = ResolutionData.AttackRoll.Sum(d => d.Result);
        stringBuilder.AppendFormat(
            localizationService.GetString(templateKey),
            player.Name,
            attacker.Model,
            WeaponData.Name,
            targetPlayer.Name,
            target.Model,
            ResolutionData.ToHitNumber,
            rollTotal).AppendLine();
    }

    private static void AppendDamageSummary(
        StringBuilder stringBuilder,
        ILocalizationService localizationService,
        AttackHitLocationsData hitLocationsData) =>
        stringBuilder.AppendFormat(
            localizationService.GetString("Command_WeaponAttackResolution_TotalDamage"),
            hitLocationsData.TotalDamage).AppendLine();

    /// <summary>Cluster weapons report the roll and the number of missiles that hit.</summary>
    private static void AppendClusterRoll(
        StringBuilder stringBuilder,
        ILocalizationService localizationService,
        AttackHitLocationsData hitLocationsData)
    {
        if (hitLocationsData.ClusterRoll.Count <= 1) return;

        var clusterRollTotal = hitLocationsData.ClusterRoll.Sum(d => d.Result);
        stringBuilder.AppendFormat(
            localizationService.GetString("Command_WeaponAttackResolution_ClusterRoll"),
            clusterRollTotal).AppendLine();

        if (hitLocationsData.MissilesHit <= 0) return;

        stringBuilder.AppendFormat(
            localizationService.GetString("Command_WeaponAttackResolution_MissilesHit"),
            hitLocationsData.MissilesHit).AppendLine();
    }

    private static void AppendHitLocations(
        StringBuilder stringBuilder,
        ILocalizationService localizationService,
        AttackHitLocationsData hitLocationsData)
    {
        if (hitLocationsData.HitLocations.Count == 0) return;

        stringBuilder.AppendLine(localizationService.GetString("Command_WeaponAttackResolution_HitLocations"));
        foreach (var hitLocation in hitLocationsData.HitLocations)
            stringBuilder.Append(hitLocation.Render(localizationService));
    }

    private static void AppendDestroyedParts(
        StringBuilder stringBuilder,
        ILocalizationService localizationService,
        IReadOnlyList<PartLocation> destroyedParts)
    {
        stringBuilder.AppendLine(localizationService.GetString("Command_WeaponAttackResolution_DestroyedParts"));
        foreach (var location in destroyedParts)
        {
            stringBuilder.AppendFormat(
                localizationService.GetString("Command_WeaponAttackResolution_DestroyedPart"),
                localizationService.GetString($"MechPart_{location}")).AppendLine();
        }
    }
}
