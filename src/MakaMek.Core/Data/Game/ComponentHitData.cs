using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Data.Game;

public record ComponentHitData
{
    public required int Slot { get; init; }
    public required MakaMekComponent Type { get; init; }
    public int ExplosionDamage { get; init; }
    public LocationDamageData[] ExplosionDamageDistribution  { get; init; } = [];

    public static ComponentHitData CreateComponentHitData(
        UnitPart part,
        int slot,
        IDamageTransferCalculator damageTransferCalculator)
    {
        if (part.Unit == null) throw new ArgumentException("Detached part", nameof(part));
        var component = part.GetComponentAtSlot(slot);
        if (component == null) throw new ArgumentException("Invalid slot", nameof(slot));
        var explosionDamage = component.GetExplosionDamage();
        var distribution = explosionDamage > 0
            ? damageTransferCalculator
                .CalculateExplosionDamage(part.Unit, part.Location, explosionDamage)
                .ToArray()
            : [];
        return new ComponentHitData
        {
            Slot = slot,
            Type = component.ComponentType,
            ExplosionDamage = explosionDamage,
            ExplosionDamageDistribution = distribution
        };
    }
}