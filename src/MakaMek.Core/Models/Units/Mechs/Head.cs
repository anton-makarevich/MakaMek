using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Game;

namespace Sanet.MakaMek.Core.Models.Units.Mechs;

public class Head : UnitPart
{
    public Head(string name, int maxArmor, int maxStructure) 
        : base(name, PartLocation.Head, maxArmor, maxStructure, 6)
    {
        // Add default components
        TryAddComponent(new LifeSupport(), LifeSupport.DefaultMountSlots);
        TryAddComponent(new Sensors(), Sensors.DefaultMountSlots);
        TryAddComponent(new Cockpit(), Cockpit.DefaultMountSlots);
    }

    internal override bool CanBeBlownOff => true;

    public override bool BlowOff()
    {
        var isBlownOff = base.BlowOff();
        if (isBlownOff)
            Unit?.Pilot?.Kill();
        return isBlownOff;
    }
    
    public override int ApplyDamage(int damage, HitDirection direction, bool isExplosion = false)
    {
        if (damage > 0)
            Unit?.Pilot?.Hit();
        return base.ApplyDamage(damage, direction, isExplosion);
    }

    public override IReadOnlyList<WeaponConfigurationOptions> GetWeaponsConfigurationOptions()
    {
        if (Unit is not Mech mech || mech.Position == null || !mech.CanRotateTorso)
        {
            return [];
        }

        var availableDirections = MechPartsExtensions.GetAvailableTorsoRotationDirections(
            mech.Position.Facing,
            mech.PossibleTorsoRotation);

        if (availableDirections.Count == 0)
        {
            return [];
        }

        return
        [
            new WeaponConfigurationOptions(WeaponConfigurationType.TorsoRotation, availableDirections)
        ];
    }
}