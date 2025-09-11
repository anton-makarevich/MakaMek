using Sanet.MakaMek.Core.Exceptions;
using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components;

public abstract class Component : IManufacturedItem
{
    protected Component(string name, int[] slots, int size = 1, string manufacturer = "Unknown", int healthPoints = 1)
    {
        Name = name;
        MountedAtSlots = slots;
        IsFixed = slots.Length > 0;
        Size = IsFixed
        ? slots.Length
        : size;
        Manufacturer = manufacturer;
        HealthPoints = healthPoints;
    }

    public string Name { get; }
    public int[] MountedAtSlots { get; private set; }
    public bool IsActive { get; protected set; } = true;
    
    public bool IsAvailable => IsActive 
                               && !IsDestroyed 
                               && IsMounted 
                               && !MountedOn!.IsDestroyed;
    
    public int Size { get; }
    public string Manufacturer { get; }
    public bool IsFixed { get; }
    public int BattleValue { get; protected set; }

    // Reference to the part this component is mounted on
    public UnitPart? MountedOn { get; private set; }

    // Component type property for mapping to MakaMekComponent enum
    public abstract MakaMekComponent ComponentType { get; }

    // Slot positioning
    public bool IsMounted => MountedAtSlots.Length > 0 && MountedOn != null;

    public void Mount(int[] slots, UnitPart mountLocation)
    {
        if (IsMounted) return;
        if (slots.Length != Size)
        {
            throw new ComponentException($"Component {Name} requires {Size} slots.");
        }
        
        MountedAtSlots = slots;
        MountedOn = mountLocation;
    }

    public void UnMount()
    {
        if (IsFixed)
        {
            throw new ComponentException("Fixed components cannot be unmounted.");
        }
        if (!IsMounted) return;
        
        MountedAtSlots = [];
        MountedOn = null;
    }

    public virtual void Hit()
    {
        Hits++;
        if (CanExplode && !HasExploded)
        {
            HasExploded = true;
            MountedOn?.Unit?.Pilot?.ExplosionHit();
        }
    }

    public int HealthPoints { get; }
    public int Hits { get; private set; }
    public bool IsDestroyed => Hits >= HealthPoints;

    public virtual void Activate() => IsActive = true;
    public virtual void Deactivate() => IsActive = false;
    
    // Helper method to get the location of this component
    public PartLocation? GetLocation() => MountedOn?.Location;
    
    public virtual bool IsRemovable => true;

    public ComponentStatus Status
    {
        get
        {
            if (IsDestroyed)
                return ComponentStatus.Destroyed;
            if (!IsMounted)
                return ComponentStatus.Removed;
            if (!IsActive)
                return ComponentStatus.Deactivated;
            if (MountedOn is { IsDestroyed: true })
                return ComponentStatus.Lost;
            if (Hits>0 && Hits<HealthPoints)
                return ComponentStatus.Damaged;
            return ComponentStatus.Active;
        }
    }
    
    // Explosion-related properties and methods
    public virtual bool CanExplode => false;
    public virtual int GetExplosionDamage() => 0;
    public bool HasExploded { get; protected set; }
}
