using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

/// <summary>
/// Calculates structure damage distribution without applying it to units
/// </summary>
public class DamageTransferCalculator : IDamageTransferCalculator
{
    public IReadOnlyList<LocationDamageData> CalculateStructureDamage(
        Unit unit,
        PartLocation initialLocation,
        int totalDamage,
        HitDirection hitDirection)
    {
        return CalculateDamageDistribution(unit, initialLocation, totalDamage, hitDirection);
    }

    public IReadOnlyList<LocationDamageData> CalculateExplosionDamage(
        Unit unit,
        PartLocation initialLocation,
        int totalDamage)
    {
        return CalculateDamageDistribution(unit, initialLocation, totalDamage, HitDirection.Front, true);
    }
    
    private List<LocationDamageData> CalculateDamageDistribution(Unit unit, PartLocation initialLocation, int totalDamage,
        HitDirection hitDirection, bool isExplosion = false)
    {
        var damageDistribution = new List<LocationDamageData>();
        var remainingDamage = totalDamage;
        PartLocation? currentLocation = initialLocation;

        while (remainingDamage > 0 && currentLocation.HasValue)
        {
            var part = unit.Parts.FirstOrDefault(p => p.Location == currentLocation.Value);
            if (part == null)
                break;

            var locationDamage = isExplosion
                ? CalculateExplosionLocationDamage(part, remainingDamage)
                : CalculateLocationDamage(part, remainingDamage, hitDirection);
            damageDistribution.Add(locationDamage);

            // Calculate remaining damage after this location
            remainingDamage -= (locationDamage.ArmorDamage + locationDamage.StructureDamage);

            // If a location is destroyed and there's remaining damage, transfer to the next location
            if (locationDamage.IsLocationDestroyed && remainingDamage > 0)
            {
                currentLocation = unit.GetTransferLocation(currentLocation.Value);
            }
            else
            {
                break;
            }
        }

        return damageDistribution;
    }

    private LocationDamageData CalculateLocationDamage(UnitPart part, int incomingDamage, HitDirection hitDirection)
    {
        var armorDamage = 0;
        var structureDamage = 0;
        var remainingDamage = incomingDamage;

        // Calculate armor damage first
        var (availableArmor, isRearArmor) = GetAvailableArmor(part, hitDirection);
        if (availableArmor > 0)
        {
            armorDamage = Math.Min(remainingDamage, availableArmor);
            remainingDamage -= armorDamage;
        }

        // Calculate structure damage if armor is depleted
        if (remainingDamage > 0)
        {
            var availableStructure = part.CurrentStructure;
            structureDamage = Math.Min(remainingDamage, availableStructure);
        }

        var locationDestroyed = structureDamage >= part.CurrentStructure;

        return new LocationDamageData(
            part.Location,
            armorDamage,
            structureDamage,
            locationDestroyed,
            isRearArmor
        );
    }

    private LocationDamageData CalculateExplosionLocationDamage(UnitPart part, int incomingDamage)
    {
        // Explosion damage bypasses armor entirely
        var availableStructure = part.CurrentStructure;
        var structureDamage = Math.Min(incomingDamage, availableStructure);
        var locationDestroyed = structureDamage >= part.CurrentStructure;

        return new LocationDamageData(
            part.Location,
            0,
            structureDamage,
            locationDestroyed 
        );
    }

    private (int, bool) GetAvailableArmor(UnitPart part, HitDirection hitDirection)
    {
        return part switch
        {
            Torso torso when hitDirection == HitDirection.Rear => (torso.CurrentRearArmor, true),
            _ => (part.CurrentArmor, false)
        };
    }
}
