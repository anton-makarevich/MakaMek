using System.Text;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Client;

public record struct WeaponAttackDeclarationCommand : IClientCommand
{
    public required Guid GameOriginId { get; set; }
    public required Guid AttackerId { get; init; }
    public required List<WeaponTargetData> WeaponTargets { get; init; }
    public required Guid PlayerId { get; init; }
    public DateTime Timestamp { get; set; }

    public string Render(ILocalizationService localizationService, IGame game)
    {
        var command = this;
        var player = game.Players.FirstOrDefault(p => p.Id == command.PlayerId);
        var attacker = player?.Units.FirstOrDefault(u => u.Id == command.AttackerId);

        if (attacker == null || player == null) return string.Empty;

        if (WeaponTargets.Count == 0)
        {
            var noAttacksTemplate = localizationService.GetString("Command_WeaponAttackDeclaration_NoAttacks");
            return string.Format(noAttacksTemplate, player.Name, attacker.Model);
        }

        // Build a dictionary of actual target units by the IDs provided in WeaponTargets
        var targetIds = WeaponTargets.Select(wt => wt.TargetId).ToHashSet();
        var unitsById = game.Players
            .SelectMany(p => p.Units)
            .Where(u => targetIds.Contains(u.Id))
            .ToDictionary(u => u.Id, u => u);

        // Early exit if none of the provided target IDs correspond to actual units
        if (unitsById.Count == 0)
        {
            var noAttacksTemplate = localizationService.GetString("Command_WeaponAttackDeclaration_NoAttacks");
            return string.Format(noAttacksTemplate, player.Name, attacker.Model);
        }

        var stringBuilder = new StringBuilder();
        var headerTemplate = localizationService.GetString("Command_WeaponAttackDeclaration_Header");
        stringBuilder.AppendFormat(headerTemplate, player.Name, attacker.Model).AppendLine();

        var weaponLineTemplate = localizationService.GetString("Command_WeaponAttackDeclaration_WeaponLine");

        foreach (var weaponTarget in WeaponTargets)
        {
            if (!unitsById.TryGetValue(weaponTarget.TargetId, out var targetUnit))
                continue;

            var targetPlayer = targetUnit.Owner;
            if (targetPlayer == null) continue;

            // Get weapon name from the actual weapon component
            var primaryAssignment = weaponTarget.Weapon.Assignments.FirstOrDefault();
            var weapon = primaryAssignment != null ?
                attacker?.GetMountedComponentAtLocation<Weapon>(primaryAssignment.Location, primaryAssignment.Slots.ToArray()) :
                null;
            var weaponName = weapon?.Name ?? "Unknown Weapon";

            stringBuilder.AppendFormat(weaponLineTemplate,
                weaponName,
                targetPlayer.Name,
                targetUnit.Model).AppendLine();
        }

        return stringBuilder.ToString().TrimEnd();
    }
}