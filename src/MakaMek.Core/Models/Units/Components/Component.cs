using Sanet.MakaMek.Core.Exceptions;
using Sanet.MakaMek.Core.Data.Community;

namespace Sanet.MakaMek.Core.Models.Units.Components;

public abstract class Component : IManufacturedItem
{
    protected Component(string name, int[] slots, int size = 1, string manufacturer = "Unknown")
    {
        Name = name;
        MountedAtSlots = slots;
        IsFixed = slots.Length > 0;
        Size = IsFixed
        ? slots.Length
        : size;
        Manufacturer = manufacturer;
    }

    public string Name { get; }
    public int[] MountedAtSlots { get; private set; }
    public bool IsDestroyed { get; private set; }
    public bool IsActive { get; protected set; } = true;
    
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
        IsDestroyed = true;
    }

    public int Hits { get; private set; }

    public virtual void Activate() => IsActive = true;
    public virtual void Deactivate() => IsActive = false;
    
    // Helper method to get the location of this component
    public PartLocation? GetLocation() => MountedOn?.Location;
    
    public virtual bool IsRemovable => true;
}
