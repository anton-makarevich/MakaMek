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
        var weapon = attacker?.GetMountedComponentAtLocation<Weapon>(WeaponData.Location,WeaponData.Slots);
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

            // Add an attack direction if available
            if (ResolutionData.AttackDirection.HasValue)
            {
                var directionKey = $"AttackDirection_{ResolutionData.AttackDirection.Value}";
                var directionString = localizationService.GetString(directionKey);
                
                stringBuilder.AppendLine(string.Format(
                    localizationService.GetString("Command_WeaponAttackResolution_Direction"),
                    directionString));
            }

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

                stringBuilder.AppendLine(string.Format(
                    localizationService.GetString("Command_WeaponAttackResolution_MissilesHit"),
                    ResolutionData.HitLocationsData.MissilesHit));
            }

            // Add hit locations with damage
            if (ResolutionData.HitLocationsData.HitLocations.Count <= 0) return stringBuilder.ToString().TrimEnd();

            stringBuilder.AppendLine(localizationService.GetString("Command_WeaponAttackResolution_HitLocations"));

            foreach (var hitLocation in ResolutionData.HitLocationsData.HitLocations)
            {
                var locationRollTotal = hitLocation.LocationRoll.Sum(d => d.Result);
                
                // If there was a location transfer, show both the initial and final locations
                if (hitLocation.InitialLocation.HasValue && hitLocation.InitialLocation.Value != hitLocation.Location)
                {
                    stringBuilder.AppendLine(string.Format(
                        localizationService.GetString("Command_WeaponAttackResolution_HitLocationTransfer"),
                        hitLocation.InitialLocation.Value,
                        hitLocation.Location,
                        hitLocation.Damage,
                        locationRollTotal));
                }
                else
                {
                    stringBuilder.AppendLine(string.Format(
                        localizationService.GetString("Command_WeaponAttackResolution_HitLocation"),
                        hitLocation.Location,
                        hitLocation.Damage,
                        locationRollTotal));
                }
                
                // Process all critical hits for this hit location
                if (hitLocation.CriticalHits == null || !hitLocation.CriticalHits.Any())
                    continue;
                
                // Process all critical hits in order
                foreach (var criticalHit in hitLocation.CriticalHits)
                {
                    // Show location if different from the primary hit location
                    if (criticalHit.Location != hitLocation.Location)
                    {
                        stringBuilder.AppendLine(string.Format(
                            localizationService.GetString("Command_WeaponAttackResolution_LocationCriticals"),
                            criticalHit.Location));
                    }
                    
                    stringBuilder.AppendLine(string.Format(
                        localizationService.GetString("Command_WeaponAttackResolution_CritRoll"),
                        criticalHit.Roll));
                    
                    // Check if the location is blown off
                    if (criticalHit.IsBlownOff)
                    {
                        stringBuilder.AppendLine(string.Format(
                            localizationService.GetString("Command_WeaponAttackResolution_BlownOff"),
                            criticalHit.Location));
                        continue;
                    }
                    
                    stringBuilder.AppendLine(string.Format(
                        localizationService.GetString("Command_WeaponAttackResolution_NumCrits"),
                        criticalHit.NumCriticalHits));
                    
                    if (criticalHit.HitComponents == null || criticalHit.HitComponents.Length == 0)
                        continue;
                    
                    var targetUnit = game.Players.SelectMany(p => p.Units).FirstOrDefault(u => u.Id == command.TargetId);
                    var part = targetUnit?.Parts.FirstOrDefault(p => p.Location == criticalHit.Location);

                    foreach (var component in criticalHit.HitComponents)
                    {
                        var slot = component.Slot;
                        var comp = part?.GetComponentAtSlot(slot);
                        if (comp == null) continue;
                        var compName = comp.Name;

                        stringBuilder.AppendLine(string.Format(
                            localizationService.GetString("Command_WeaponAttackResolution_CriticalHit"),
                            criticalHit.Location,
                            slot + 1,
                            compName));
                        // Check if this component can explode
                        if (comp is not { CanExplode: true, HasExploded: false }) continue;
                        var damage = comp.GetExplosionDamage();
                        if (damage <= 0) continue;
                        var explosionTemplate =
                            localizationService.GetString("Command_WeaponAttackResolution_Explosion");

                        stringBuilder.AppendLine(string.Format(explosionTemplate,
                            compName,
                            damage));
                    }
                }
            }

            // Add destroyed parts information
            if (ResolutionData.DestroyedParts != null && ResolutionData.DestroyedParts.Any())
            {
                stringBuilder.AppendLine(localizationService.GetString("Command_WeaponAttackResolution_DestroyedParts"));
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
