using System.Text;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Game.Players;
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
    /// <summary>
    /// Identifies this one resolution. Each arm of a two-arm punch gets its own. A game applies the
    /// damage of a resolution once, however many times the command is delivered.
    /// </summary>
    public required Guid ResolutionId { get; init; }

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
        var target = FindUnit(game, command.TargetId);

        if (player == null || attacker == null)
            return string.Empty;

        if (ResolutionData.Charge is { Outcome: ChargeOutcome.AutoFail } autoFail)
            return RenderAutoFailCharge(localizationService, player, attacker, target, autoFail);

        var targetPlayer = target?.Owner;
        if (target == null || targetPlayer == null)
            return string.Empty;

        var stringBuilder = new StringBuilder();
        AppendAttackSummary(stringBuilder, localizationService, player, attacker, target, targetPlayer);
        AppendChargeOutcome(stringBuilder, localizationService, game);

        var resolution = ResolutionData.AttackResolution;
        if (!resolution.IsHit || resolution.HitLocationsData == null)
            return stringBuilder.ToString().TrimEnd();

        AppendDamage(stringBuilder, localizationService, resolution, target);
        AppendAttackerCollision(stringBuilder, localizationService, attacker);
        return stringBuilder.ToString().TrimEnd();
    }

    private static IUnit? FindUnit(IGame game, Guid unitId) =>
        game.Players.SelectMany(p => p.Units).FirstOrDefault(u => u.Id == unitId);

    private string RenderAutoFailCharge(
        ILocalizationService localizationService,
        IPlayer player,
        IUnit attacker,
        IUnit? target,
        ChargeResolutionData autoFail) =>
        string.Format(
            localizationService.GetString("Command_ChargeResolution_AutoFail"),
            player.Name,
            attacker.Model,
            target?.Model ?? autoFail.OriginalTargetId.ToString(),
            autoFail.HexesMoved);

    /// <summary>
    /// The weapon attack templates read "hits X with {2}", so the attack and limb fill that slot.
    /// A paired-arm attack such as Push has one outcome and no meaningful single limb, and a
    /// charge has no limb at all.
    /// </summary>
    private void AppendAttackSummary(
        StringBuilder stringBuilder,
        ILocalizationService localizationService,
        IPlayer player,
        IUnit attacker,
        IUnit target,
        IPlayer targetPlayer)
    {
        var attackName = GetAttackName(localizationService);
        var resolution = ResolutionData.AttackResolution;
        var rollTotal = resolution.AttackRoll.Sum(d => d.Result);

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
    }

    private string GetAttackName(ILocalizationService localizationService)
    {
        var attackTypeName = localizationService.GetString($"PhysicalAttackType_{ResolutionData.AttackType}");
        var usesLimb = ResolutionData.AttackerLimb is not null
                       && !(PhysicalAttackDefinitions.TryGet(ResolutionData.AttackType, out var definition)
                            && definition.ResolvesAsSingleAttack);

        return usesLimb
            ? $"{attackTypeName} ({localizationService.GetString($"MechPart_{ResolutionData.AttackerLimb}")})"
            : attackTypeName;
    }

    private void AppendChargeOutcome(StringBuilder stringBuilder, ILocalizationService localizationService, IGame game)
    {
        if (ResolutionData.Charge is not { } charge) return;

        stringBuilder.AppendFormat(
            localizationService.GetString("Command_ChargeResolution_Outcome"),
            localizationService.GetString($"ChargeOutcome_{charge.Outcome}"),
            charge.HexesMoved).AppendLine();

        if (!charge.IsReplacementTarget) return;

        var originalTarget = FindUnit(game, charge.OriginalTargetId);
        stringBuilder.AppendFormat(
            localizationService.GetString("Command_ChargeResolution_Replacement"),
            originalTarget?.Model ?? charge.OriginalTargetId.ToString()).AppendLine();
    }

    private static void AppendDamage(
        StringBuilder stringBuilder,
        ILocalizationService localizationService,
        AttackResolutionData resolution,
        IUnit target)
    {
        var hitLocationsData = resolution.HitLocationsData!;

        stringBuilder.AppendFormat(
            localizationService.GetString("Command_WeaponAttackResolution_Direction"),
            localizationService.GetString($"AttackDirection_{resolution.AttackDirection}")).AppendLine();
        stringBuilder.AppendFormat(
            localizationService.GetString("Command_WeaponAttackResolution_TotalDamage"),
            hitLocationsData.TotalDamage).AppendLine();

        AppendHitLocations(stringBuilder, localizationService, hitLocationsData.HitLocations);
        AppendDestroyedParts(stringBuilder, localizationService, resolution.DestroyedParts);

        if (resolution.UnitDestroyed)
        {
            stringBuilder.AppendFormat(
                localizationService.GetString("Command_WeaponAttackResolution_UnitDestroyed"),
                target.Model).AppendLine();
        }
    }

    private void AppendAttackerCollision(
        StringBuilder stringBuilder,
        ILocalizationService localizationService,
        IUnit attacker)
    {
        if (ResolutionData.Charge?.AttackerCollision is not { } collision) return;

        stringBuilder.AppendFormat(
            localizationService.GetString("Command_ChargeResolution_AttackerCollision"),
            attacker.Model,
            collision.TotalDamage).AppendLine();

        AppendHitLocations(stringBuilder, localizationService, collision.HitLocations);
        AppendDestroyedParts(stringBuilder, localizationService, ResolutionData.Charge.AttackerDestroyedParts);

        if (ResolutionData.Charge.AttackerUnitDestroyed)
        {
            stringBuilder.AppendFormat(
                localizationService.GetString("Command_WeaponAttackResolution_UnitDestroyed"),
                attacker.Model).AppendLine();
        }
    }

    private static void AppendHitLocations(
        StringBuilder stringBuilder,
        ILocalizationService localizationService,
        IReadOnlyList<LocationHitData> hitLocations)
    {
        if (hitLocations.Count == 0) return;

        stringBuilder.AppendLine(localizationService.GetString("Command_WeaponAttackResolution_HitLocations"));
        foreach (var hitLocation in hitLocations)
            stringBuilder.Append(hitLocation.Render(localizationService));
    }

    private static void AppendDestroyedParts(
        StringBuilder stringBuilder,
        ILocalizationService localizationService,
        IReadOnlyList<PartLocation>? destroyedParts)
    {
        if (destroyedParts is not { Count: > 0 }) return;

        stringBuilder.AppendLine(localizationService.GetString("Command_WeaponAttackResolution_DestroyedParts"));
        foreach (var location in destroyedParts)
        {
            stringBuilder.AppendFormat(
                localizationService.GetString("Command_WeaponAttackResolution_DestroyedPart"),
                localizationService.GetString($"MechPart_{location}")).AppendLine();
        }
    }
}
