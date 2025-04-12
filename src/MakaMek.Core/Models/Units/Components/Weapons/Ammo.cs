using Sanet.MakaMek.Core.Data.Community;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons;

public class Ammo : Component
{
    private int _remainingShots;

    public Ammo(AmmoType type, int initialShots) : base($"{type} Ammo", [])
    {
        Type = type;
        _remainingShots = initialShots;
    }

    public AmmoType Type { get; }

    public int RemainingShots => _remainingShots;

    public bool UseShot()
    {
        if (_remainingShots <= 0)
            return false;

        _remainingShots--;
        return true;
    }

    public override MakaMekComponent ComponentType
    {
        get
        {
            return Type switch
            {
                AmmoType.MachineGun => MakaMekComponent.ISAmmoMG,
                AmmoType.AC2 => MakaMekComponent.ISAmmoAC2,
                AmmoType.AC5 => MakaMekComponent.ISAmmoAC5,
                AmmoType.AC10 => MakaMekComponent.ISAmmoAC10,
                AmmoType.AC20 => MakaMekComponent.ISAmmoAC20,
                AmmoType.LRM5 => MakaMekComponent.ISAmmoLRM5,
                AmmoType.LRM10 => MakaMekComponent.ISAmmoLRM10,
                AmmoType.LRM15 => MakaMekComponent.ISAmmoLRM15,
                AmmoType.LRM20 => MakaMekComponent.ISAmmoLRM20,
                AmmoType.SRM2 => MakaMekComponent.ISAmmoSRM2,
                AmmoType.SRM4 => MakaMekComponent.ISAmmoSRM4,
                AmmoType.SRM6 => MakaMekComponent.ISAmmoSRM6,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

    public override void Hit()
    {
        base.Hit();
        _remainingShots = 0;
    }
}
