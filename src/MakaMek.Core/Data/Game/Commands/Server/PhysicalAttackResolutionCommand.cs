using System.Text;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.Localization;

#pragma warning disable CA1502, CA1506, S3776

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

        if (ResolutionData.Charge is { Outcome: ChargeOutcome.AutoFail } autoFail)
        {
            return string.Format(
                localizationService.GetString("Command_ChargeResolution_AutoFail"),
                player.Name,
                attacker.Model,
                target?.Model ?? autoFail.OriginalTargetId.ToString(),
                autoFail.HexesMoved);
        }

        if (ResolutionData.Dfa is { } dfaEarly
            && RenderDfaEarlyOutcome(localizationService, player.Name, attacker.Model, target, dfaEarly)
                is { } earlyOutcome)
            return earlyOutcome;

        if (target == null || targetPlayer == null)
            return string.Empty;

        // The weapon attack templates read "hits X with {2}", so the attack and limb fill that slot.
        // A paired-arm attack such as Push has one outcome and no meaningful single limb, and a
        // charge has no limb at all.
        var attackTypeName = localizationService.GetString($"PhysicalAttackType_{ResolutionData.AttackType}");
        var usesLimb = ResolutionData.AttackerLimb is not null
                       && !(PhysicalAttackDefinitions.TryGet(ResolutionData.AttackType, out var definition)
                            && definition.ResolvesAsSingleAttack);
        var attackName = usesLimb
            ? $"{attackTypeName} ({localizationService.GetString($"MechPart_{ResolutionData.AttackerLimb}")})"
            : attackTypeName;
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

        if (ResolutionData.Dfa is { } dfa)
            AppendDfaOutcome(stringBuilder, localizationService, dfa);

        if (ResolutionData.Charge is { } charge)
        {
            stringBuilder.AppendFormat(
                localizationService.GetString("Command_ChargeResolution_Outcome"),
                localizationService.GetString($"ChargeOutcome_{charge.Outcome}"),
                charge.HexesMoved).AppendLine();

            if (charge.IsReplacementTarget)
            {
                var originalTarget = game.Players
                    .SelectMany(p => p.Units)
                    .FirstOrDefault(u => u.Id == charge.OriginalTargetId);
                stringBuilder.AppendFormat(
                    localizationService.GetString("Command_ChargeResolution_Replacement"),
                    originalTarget?.Model ?? charge.OriginalTargetId.ToString()).AppendLine();
            }
        }

        if (!resolution.IsHit || resolution.HitLocationsData == null)
            return stringBuilder.ToString().TrimEnd();

        if (ResolutionData.Dfa is { } dfaData)
            AppendDfaAttackerLegDamage(stringBuilder, localizationService, attacker, dfaData);

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

        if (ResolutionData.Charge?.AttackerCollision is { } collision)
        {
            stringBuilder.AppendFormat(
                localizationService.GetString("Command_ChargeResolution_AttackerCollision"),
                attacker.Model,
                collision.TotalDamage).AppendLine();

            if (collision.HitLocations.Count > 0)
            {
                stringBuilder.AppendLine(localizationService.GetString("Command_WeaponAttackResolution_HitLocations"));
                foreach (var hitLocation in collision.HitLocations)
                    stringBuilder.Append(hitLocation.Render(localizationService));
            }

            if (ResolutionData.Charge.AttackerDestroyedParts is { Count: > 0 } attackerParts)
            {
                stringBuilder.AppendLine(localizationService.GetString("Command_WeaponAttackResolution_DestroyedParts"));
                foreach (var location in attackerParts)
                {
                    stringBuilder.AppendFormat(
                        localizationService.GetString("Command_WeaponAttackResolution_DestroyedPart"),
                        localizationService.GetString($"MechPart_{location}")).AppendLine();
                }
            }

            if (ResolutionData.Charge.AttackerUnitDestroyed)
            {
                stringBuilder.AppendFormat(
                    localizationService.GetString("Command_WeaponAttackResolution_UnitDestroyed"),
                    attacker.Model).AppendLine();
            }
        }

        return stringBuilder.ToString().TrimEnd();
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
