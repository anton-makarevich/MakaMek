using Sanet.MakaMek.Core.Events;
using Sanet.MakaMek.Core.Models.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units;

public abstract class UnitPart
{
    protected UnitPart(string name, PartLocation location, int maxArmor, int maxStructure, int slots)
    {
        Name = name;
        Location = location;
        CurrentArmor = MaxArmor = maxArmor;
        CurrentStructure = MaxStructure = maxStructure;
        TotalSlots = slots;
        _components = [];
        _hitSlots = [];
    }

    public string Name { get; }
    public PartLocation Location { get; }
    
    // Reference to parent unit
    public Unit? Unit { get; internal set; }
    
    // Armor and Structure
    public int MaxArmor { get; }
    public int CurrentArmor { get; private set; }
    public int MaxStructure { get; }
    public int CurrentStructure { get; private set; }
    
    // Slots management
    public int TotalSlots { get; }
    public int UsedSlots => _components.Sum(c => c.Size);
    public int AvailableSlots => TotalSlots - UsedSlots;
    
    // Track which slots have been hit by critical hits
    private readonly HashSet<int> _hitSlots;
    public IReadOnlySet<int> HitSlots => _hitSlots;
    
    // Abstract property to be implemented by derived classes
    internal abstract bool CanBeBlownOff { get; }
    
    // Property to track if the part is blown off, with private setter
    public bool IsBlownOff { get; private set; }
    
    // A part is destroyed if either structure is depleted or it's blown off
    public bool IsDestroyed => CurrentStructure <= 0 
                               || IsBlownOff
                               || DamageTransferPart?.IsDestroyed == true;
    
    // Components installed in this part
    private readonly List<Component> _components;
    public IReadOnlyList<Component> Components => _components;

    private int FindMountLocation(int size)
    {
        var occupiedSlots = _components
            .Where(c => c.IsMounted)
            .SelectMany(c => c.MountedAtSlots)
            .ToHashSet();

        return Enumerable.Range(0, TotalSlots - size + 1)
            .FirstOrDefault(i => Enumerable.Range(i, size).All(slot => !occupiedSlots.Contains(slot)), -1);
    }

    private bool CanAddComponent(Component component)
    {
        if (component.Size > AvailableSlots)
            return false;

        // Check if any required slots would be out of bounds
        if (component.MountedAtSlots.Any(s => s >= TotalSlots))
            return false;

        // Check if any of the required slots are already occupied
        var occupiedSlots = _components.Where(c => c.IsMounted)
            .SelectMany(c => c.MountedAtSlots)
            .ToHashSet();
        
        return !component.MountedAtSlots.Intersect(occupiedSlots).Any();
    }

    public bool TryAddComponent(Component component, int[]? slots=null)
    {
        if (component.IsFixed)
        {
            if (!CanAddComponent(component))
            {
                return false;
            }

            _components.Add(component);
            // Update the component with its mount location
            component.Mount(component.MountedAtSlots, this);
            return true;
        }

        var slotToMount = slots!=null
            ? slots[0]
            : FindMountLocation(component.Size);
        if (slotToMount == -1)
        {
            return false;
        }

        // Use the new Mount method that includes UnitPart reference
        component.Mount(Enumerable.Range(slotToMount, component.Size).ToArray(), this);
        _components.Add(component);
        return true;
    }

    public Component? GetComponentAtSlot(int slot)
    {
        return _components.FirstOrDefault(c => c.IsMounted && c.MountedAtSlots.Contains(slot));
    }

    public virtual int ApplyDamage(int damage, HitDirection direction = HitDirection.Front)
    {
        // First reduce armor
        var remainingDamage =  ReduceArmor(damage,direction);

        // Then apply to structure if armor is depleted
        if (remainingDamage > 0)
        {
            if (CurrentStructure >= remainingDamage)
            {
                CurrentStructure -= remainingDamage;
                Unit?.AddEvent(new UiEvent(UiEventType.StructureDamage,Name,remainingDamage.ToString()));
                return 0;
            }
            remainingDamage -= CurrentStructure;
            Unit?.AddEvent(new UiEvent(UiEventType.StructureDamage,Name,CurrentStructure.ToString()));
            CurrentStructure = 0;
            Unit?.AddEvent(new UiEvent(UiEventType.LocationDestroyed,Name));
            return remainingDamage; // Return excess damage for transfer
        }

        return 0;
    }

    protected virtual int ReduceArmor(int damage, HitDirection direction)
    {
        if (CurrentArmor <= 0) return damage;
        if (CurrentArmor >= damage)
        {
            CurrentArmor -= damage;
            Unit?.AddEvent(new UiEvent(UiEventType.ArmorDamage,Name,damage.ToString()));
            return 0;
        }
        damage -= CurrentArmor;
        Unit?.AddEvent(new UiEvent(UiEventType.ArmorDamage,Name,CurrentArmor.ToString()));
        CurrentArmor = 0;

        return damage;
    }

    public T? GetComponent<T>() where T : Component
    {
        return _components.OfType<T>().FirstOrDefault();
    }

    public IEnumerable<T> GetComponents<T>() where T : Component
    {
        return _components.OfType<T>();
    }

    /// <summary>
    /// Removes a component from this part, unmounting it if necessary
    /// </summary>
    /// <param name="component">The component to remove</param>
    /// <returns>True if the component was successfully removed, false otherwise</returns>
    public bool RemoveComponent(Component component)
    {
        if (!_components.Contains(component))
        {
            return false;
        }

        if (component is { IsMounted: true, IsFixed: false })
        {
            component.UnMount();
        }

        return _components.Remove(component);
    }

    public PartLocation? GetNextTransferLocation()
    {
        return Unit?.GetTransferLocation(Location);
    }
    
    protected UnitPart? DamageTransferPart => Unit?.Parts.FirstOrDefault(p => p.Location == GetNextTransferLocation()); 
    
    /// <summary>
    /// Blows off this part as a result of a critical hit
    /// </summary>
    /// <returns>True if the part was successfully blown off, false otherwise</returns>
    public virtual bool BlowOff()
    {
        if (!CanBeBlownOff)
            return false;
            
        IsBlownOff = true;
        return true;
    }
    
    /// <summary>
    /// Records a critical hit on a specific slot and damages the component in that slot if it exists
    /// </summary>
    /// <param name="slot">The slot index that was hit</param>
    public void CriticalHit(int slot)
    {
        _hitSlots.Add(slot);
        
        
        var component = GetComponentAtSlot(slot);


        if (component is not { IsDestroyed: false }) return;
        // Raise critical hit event
        Unit?.AddEvent(new UiEvent(UiEventType.CriticalHit, component.Name));
        component.Hit();
            
        // Raise component destroyed event if the component was destroyed by this hit
        if (component.IsDestroyed)
        {
            Unit?.AddEvent(new UiEvent(UiEventType.ComponentDestroyed, component.Name));
        }
    }
}
