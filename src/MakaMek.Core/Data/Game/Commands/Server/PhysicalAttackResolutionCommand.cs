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
        var target = game.Players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == command.TargetId);
        var targetPlayer = target?.Owner;

        if (player == null || attacker == null)
            return string.Empty;

        if (RenderEarlyOutcome(localizationService, player, attacker, target) is { } earlyOutcome)
            return earlyOutcome;

        if (target == null || targetPlayer == null)
            return string.Empty;

        var resolution = ResolutionData.AttackResolution;
        var stringBuilder = new StringBuilder();

        AppendIntro(stringBuilder, localizationService, player, attacker, targetPlayer, target, resolution);

        if (ResolutionData.Dfa is { } dfa)
            AppendDfaOutcome(stringBuilder, localizationService, dfa);

        if (ResolutionData.Charge is { } charge)
            AppendChargeOutcome(stringBuilder, localizationService, game, charge);

        if (!resolution.IsHit || resolution.HitLocationsData == null)
            return stringBuilder.ToString().TrimEnd();

        if (ResolutionData.Dfa is { } dfaData)
            AppendDfaAttackerLegDamage(stringBuilder, localizationService, attacker, dfaData);

        AppendDamageSummary(stringBuilder, localizationService, resolution, target.Model);

        if (ResolutionData.Charge is { AttackerCollision: not null } chargeWithCollision)
            AppendChargeAttackerCollision(
                stringBuilder, localizationService, attacker.Model, chargeWithCollision);

        return stringBuilder.ToString().TrimEnd();
    }

    /// <summary>
    /// The outcomes that render as a single line and need no target: a charge auto-fail and the two
    /// DFA failures. Returns null for every other resolution.
    /// </summary>
    private string? RenderEarlyOutcome(
        ILocalizationService localizationService,
        IPlayer player,
        IUnit attacker,
        IUnit? target)
    {
        if (ResolutionData.Charge is { Outcome: ChargeOutcome.AutoFail } autoFail)
        {
            return string.Format(
                localizationService.GetString("Command_ChargeResolution_AutoFail"),
                player.Name,
                attacker.Model,
                target?.Model ?? autoFail.OriginalTargetId.ToString(),
                autoFail.HexesMoved);
        }

        return ResolutionData.Dfa is { } dfa
            ? RenderDfaEarlyOutcome(localizationService, player.Name, attacker.Model, target, dfa)
            : null;
    }

    private void AppendIntro(
        StringBuilder stringBuilder,
        ILocalizationService localizationService,
        IPlayer player,
        IUnit attacker,
        IPlayer targetPlayer,
        IUnit target,
        AttackResolutionData resolution) =>
        stringBuilder.AppendFormat(
            localizationService.GetString(resolution.IsHit
                ? "Command_WeaponAttackResolution_Hit"
                : "Command_WeaponAttackResolution_Miss"),
            player.Name,
            attacker.Model,
            GetAttackName(localizationService),
            targetPlayer.Name,
            target.Model,
            resolution.ToHitNumber,
            resolution.AttackRoll.Sum(d => d.Result)).AppendLine();

    /// <summary>
    /// The attack name as it reads in the log: the type, plus the limb when the attack is limb-based.
    /// The weapon attack templates read "hits X with {2}", so the attack and limb fill that slot. A
    /// paired-arm attack such as Push has one outcome and no meaningful single limb, and a charge or
    /// a DFA has no limb at all.
    /// </summary>
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

    private static void AppendChargeOutcome(
        StringBuilder stringBuilder,
        ILocalizationService localizationService,
        IGame game,
        ChargeResolutionData charge)
    {
        stringBuilder.AppendFormat(
            localizationService.GetString("Command_ChargeResolution_Outcome"),
            localizationService.GetString($"ChargeOutcome_{charge.Outcome}"),
            charge.HexesMoved).AppendLine();

        if (!charge.IsReplacementTarget) return;

        var originalTarget = game.Players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == charge.OriginalTargetId);
        stringBuilder.AppendFormat(
            localizationService.GetString("Command_ChargeResolution_Replacement"),
            originalTarget?.Model ?? charge.OriginalTargetId.ToString()).AppendLine();
    }

    /// <summary>
    /// The direction, the total damage and the per-location detail of the target's damage, then the
    /// destruction metadata the resolution carries.
    /// </summary>
    private static void AppendDamageSummary(
        StringBuilder stringBuilder,
        ILocalizationService localizationService,
        AttackResolutionData resolution,
        string targetModel)
    {
        stringBuilder.AppendFormat(
            localizationService.GetString("Command_WeaponAttackResolution_Direction"),
            localizationService.GetString($"AttackDirection_{resolution.AttackDirection}")).AppendLine();
        stringBuilder.AppendFormat(
            localizationService.GetString("Command_WeaponAttackResolution_TotalDamage"),
            resolution.HitLocationsData!.TotalDamage).AppendLine();

        AppendHitLocations(stringBuilder, localizationService, resolution.HitLocationsData.HitLocations);

        if (resolution.DestroyedParts is { Count: > 0 } destroyedParts)
            AppendDestroyedParts(stringBuilder, localizationService, destroyedParts);

        if (resolution.UnitDestroyed)
        {
            stringBuilder.AppendFormat(
                localizationService.GetString("Command_WeaponAttackResolution_UnitDestroyed"),
                targetModel).AppendLine();
        }
    }

    private static void AppendChargeAttackerCollision(
        StringBuilder stringBuilder,
        ILocalizationService localizationService,
        string attackerModel,
        ChargeResolutionData charge)
    {
        if (charge.AttackerCollision is not { } collision) return;

        stringBuilder.AppendFormat(
            localizationService.GetString("Command_ChargeResolution_AttackerCollision"),
            attackerModel,
            collision.TotalDamage).AppendLine();

        AppendHitLocations(stringBuilder, localizationService, collision.HitLocations);

        if (charge.AttackerDestroyedParts is { Count: > 0 } attackerParts)
            AppendDestroyedParts(stringBuilder, localizationService, attackerParts);

        if (charge.AttackerUnitDestroyed)
        {
            stringBuilder.AppendFormat(
                localizationService.GetString("Command_WeaponAttackResolution_UnitDestroyed"),
                attackerModel).AppendLine();
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

    private static string? RenderDfaEarlyOutcome(
        ILocalizationService localizationService,
        string playerName,
        string attackerModel,
        IUnit? target,
        DfaResolutionData dfa) =>
        dfa.Outcome switch
        {
            DfaOutcome.AutoFail => string.Format(
                localizationService.GetString("Command_DfaResolution_AutoFail"),
                playerName,
                attackerModel,
                target?.Model ?? dfa.OriginalTargetId.ToString()),
            DfaOutcome.AttackerDestroyedNoLanding => string.Format(
                localizationService.GetString("Command_DfaResolution_AttackerDestroyed"),
                playerName,
                attackerModel),
            _ => null
        };

    private static void AppendDfaOutcome(
        StringBuilder stringBuilder,
        ILocalizationService localizationService,
        DfaResolutionData dfa) =>
        stringBuilder.AppendFormat(
            localizationService.GetString("Command_DfaResolution_Outcome"),
            localizationService.GetString($"DfaOutcome_{dfa.Outcome}")).AppendLine();

    private static void AppendDfaAttackerLegDamage(
        StringBuilder stringBuilder,
        ILocalizationService localizationService,
        IUnit attacker,
        DfaResolutionData dfa)
    {
        if (dfa.AttackerLegHitLocations is not { } attackerLegs) return;

        stringBuilder.AppendFormat(
            localizationService.GetString("Command_DfaResolution_AttackerLegDamage"),
            attacker.Model,
            attackerLegs.TotalDamage).AppendLine();

        if (attackerLegs.HitLocations.Count > 0)
        {
            stringBuilder.AppendLine(localizationService.GetString("Command_WeaponAttackResolution_HitLocations"));
            foreach (var hitLocation in attackerLegs.HitLocations)
                stringBuilder.Append(hitLocation.Render(localizationService));
        }

        if (dfa.AttackerDestroyedParts is { Count: > 0 } dfaAttackerParts)
        {
            stringBuilder.AppendLine(localizationService.GetString("Command_WeaponAttackResolution_DestroyedParts"));
            foreach (var location in dfaAttackerParts)
            {
                stringBuilder.AppendFormat(
                    localizationService.GetString("Command_WeaponAttackResolution_DestroyedPart"),
                    localizationService.GetString($"MechPart_{location}")).AppendLine();
            }
        }

        if (dfa.AttackerUnitDestroyed)
        {
            stringBuilder.AppendFormat(
                localizationService.GetString("Command_WeaponAttackResolution_UnitDestroyed"),
                attacker.Model).AppendLine();
        }
    }
}
