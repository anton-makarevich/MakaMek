using System.Text;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Server;

public record struct WeaponAttackResolutionCommand : IGameCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid AttackerId { get; init; }
    public required WeaponData WeaponData { get; init; }
    public required Guid TargetId { get; init; }
    public required AttackResolutionData ResolutionData { get; init; }

    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }

    public string Render(ILocalizationService localizationService, IGame game)
    {
        var command = this;
        var player = game.Players.FirstOrDefault(p => p.Id == command.PlayerId);
        var attacker = player?.Units.FirstOrDefault(u => u.Id == command.AttackerId);
        var weapon = attacker?.GetMountedComponentAtLocation<Weapon>(WeaponData.Location, WeaponData.Slots);
        var target = game.Players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == command.TargetId);
        var targetPlayer = target?.Owner;

        if (player == null || attacker == null || weapon == null || target == null || targetPlayer == null)
        {
            return string.Empty;
        }

        var rollTotal = ResolutionData.AttackRoll.Sum(d => d.Result);
        var stringBuilder = new StringBuilder();

        if (ResolutionData.IsHit)
        {
            var hitTemplate = localizationService.GetString("Command_WeaponAttackResolution_Hit");
            stringBuilder.AppendLine(string.Format(hitTemplate,
                player.Name,
                attacker.Name,
                weapon.Name,
                targetPlayer.Name,
                target.Name,
                ResolutionData.ToHitNumber,
                rollTotal));

            // Add an attack direction  
            var directionKey = $"AttackDirection_{ResolutionData.AttackDirection}";
            var directionString = localizationService.GetString(directionKey);

            stringBuilder.AppendLine(string.Format(
                localizationService.GetString("Command_WeaponAttackResolution_Direction"),
                directionString));


            // Add damage information if hit
            if (ResolutionData.HitLocationsData == null) return stringBuilder.ToString().TrimEnd();
            // Add total damage
            stringBuilder.AppendLine(string.Format(
                localizationService.GetString("Command_WeaponAttackResolution_TotalDamage"),
                ResolutionData.HitLocationsData.TotalDamage));

            // Add missiles hit information for cluster weapons
            if (ResolutionData.HitLocationsData.ClusterRoll.Count > 1)
            {
                // Add cluster roll information
                if (ResolutionData.HitLocationsData.ClusterRoll.Count > 0)
                {
                    var clusterRollTotal = ResolutionData.HitLocationsData.ClusterRoll.Sum(d => d.Result);
                    stringBuilder.AppendLine(string.Format(
                        localizationService.GetString("Command_WeaponAttackResolution_ClusterRoll"),
                        clusterRollTotal));
                }

                if (ResolutionData.HitLocationsData.MissilesHit > 0)
                {
                    stringBuilder.AppendLine(string.Format(
                        localizationService.GetString("Command_WeaponAttackResolution_MissilesHit"),
                        ResolutionData.HitLocationsData.MissilesHit));
                }
            }

            // Add hit locations with damage
            if (ResolutionData.HitLocationsData.HitLocations.Count > 0)
            {

                stringBuilder.AppendLine(localizationService.GetString("Command_WeaponAttackResolution_HitLocations"));

                // Use the Render method for each hit location
                foreach (var hitLocation in ResolutionData.HitLocationsData.HitLocations)
                {
                    stringBuilder.Append(hitLocation.Render(localizationService, target));
                }
            }

            // Add destroyed parts information
            if (ResolutionData.DestroyedParts != null && ResolutionData.DestroyedParts.Any())
            {
                stringBuilder.AppendLine(
                    localizationService.GetString("Command_WeaponAttackResolution_DestroyedParts"));
                foreach (var location in ResolutionData.DestroyedParts)
                {
                    // Get the localized part name
                    var partNameKey = $"MechPart_{location}";
                    var partName = localizationService.GetString(partNameKey);

                    stringBuilder.AppendLine(string.Format(
                        localizationService.GetString("Command_WeaponAttackResolution_DestroyedPart"),
                        partName));
                }
            }

            // Add unit destruction information
            if (ResolutionData.UnitDestroyed)
            {
                stringBuilder.AppendLine(string.Format(
                    localizationService.GetString("Command_WeaponAttackResolution_UnitDestroyed"),
                    target.Name));
            }
        }
        else
        {
            // Miss case
            var missTemplate = localizationService.GetString("Command_WeaponAttackResolution_Miss");
            stringBuilder.AppendLine(string.Format(missTemplate,
                player.Name,
                attacker.Name,
                weapon.Name,
                targetPlayer.Name,
                target.Name,
                ResolutionData.ToHitNumber,
                rollTotal));
        }

        return stringBuilder.ToString().TrimEnd();
    }
}