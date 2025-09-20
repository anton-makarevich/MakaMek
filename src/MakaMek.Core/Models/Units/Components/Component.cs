using Sanet.MakaMek.Core.Exceptions;
using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components;

public abstract class Component : IManufacturedItem
{
    private readonly List<CriticalSlotAssignment> _slotAssignments = [];

    protected Component(ComponentDefinition definition, ComponentData? componentData = null)
    {
        // Use ComponentData name override if available, otherwise use definition name
        Name = definition.Name;
        Size = definition.Size;
        // Use ComponentData manufacturer if available, otherwise use definition default
        Manufacturer = "Unknown";
        HealthPoints = definition.HealthPoints;
        BattleValue = definition.BattleValue;
        IsRemovable = definition.IsRemovable;
        ComponentType = definition.ComponentType;

        // Restore mutable state if provided
        if (componentData == null) return;
        if (!string.IsNullOrEmpty(componentData.Name))
            Name = componentData.Name;
        if (!string.IsNullOrEmpty(componentData.Manufacturer))
            Manufacturer = componentData.Manufacturer;
        Hits = componentData.Hits;
        IsActive = componentData.IsActive;
        HasExploded = componentData.HasExploded;
    }

    public string Name { get; }
    public int[] MountedAtSlots =>SlotAssignments
        .SelectMany(a => a.Slots)
        .OrderBy(slot => slot)
        .ToArray();
    
    public bool IsActive { get; private set; } = true;

    public bool IsAvailable => IsActive
                               && !IsDestroyed
                               && IsMounted
                               && !SlotAssignments.Any(a => a.UnitPart.IsDestroyed);

    public int Size { get; }
    public string Manufacturer { get; }
    public int BattleValue { get; protected init; }
    public bool IsRemovable { get; protected init; }

    // Multi-location slot assignments
    public IReadOnlyList<CriticalSlotAssignment> SlotAssignments => _slotAssignments;

    // Multi-location mounted parts
    public IReadOnlyList<UnitPart> MountedOn => SlotAssignments.Select(a => a.UnitPart).ToList();

    // Component type property for mapping to MakaMekComponent enum
    public MakaMekComponent ComponentType { get; }

    // component is mounted when all required slots are assigned
    public bool IsMounted => SlotAssignments.Sum(a => a.Length) == Size && SlotAssignments.Count > 0;

    public void Mount(int[] slots, UnitPart mountLocation)
    {
        Mount(new CriticalSlotAssignment
        {
            UnitPart = mountLocation,
            FirstSlot = slots[0],
            Length = slots.Length
        });
    }

    public void Mount(IEnumerable<CriticalSlotAssignment> slotAssignments)
    {
        foreach (var slotAssignment in slotAssignments)
        {
            Mount(slotAssignment);
        }
    }

    public void Mount(CriticalSlotAssignment slotAssignment)
    {
        if (IsMounted) return;
        var occupiedSlots = _slotAssignments.Sum(a=>a.Length);
        if (occupiedSlots + slotAssignment.Length > Size)
        {
            throw new ComponentException($"Component {Name} requires {Size} slots.");
        }
        
        _slotAssignments.Add(slotAssignment);
    }

    public void UnMount()
    {
        if (!IsMounted) return;

        _slotAssignments.Clear();
    }

    public virtual void Hit()
    {
        Hits++;
        if (CanExplode && !HasExploded)
        {
            HasExploded = true;
            GetPrimaryMountLocation()?.Unit?.Pilot?.ExplosionHit();
        }
    }

    public int HealthPoints { get; }
    public int Hits { get; private set; }
    public bool IsDestroyed => Hits >= HealthPoints;

    public virtual void Activate() => IsActive = true;
    public virtual void Deactivate() => IsActive = false;

    // Helper methods for multi-location components
    public IEnumerable<PartLocation> GetLocations() => SlotAssignments.Select(a => a.Location);

    // Backward compatibility methods
    public UnitPart? GetPrimaryMountLocation() => SlotAssignments.FirstOrDefault()?.UnitPart;
    public PartLocation? GetLocation() => GetPrimaryMountLocation()?.Location;

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
            // Component is lost if ANY location is destroyed (as per user's note)
            if (SlotAssignments.Any(a => a.UnitPart.IsDestroyed))
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

    /// <summary>
    /// Converts this component to ComponentData for state persistence
    /// </summary>
    public virtual ComponentData ToData()
    {
        return new ComponentData
        {
            Type = ComponentType,
            Assignments = SlotAssignments
                .Select(assignment => new LocationSlotAssignment(
                    assignment.Location,
                    assignment.FirstSlot,
                    assignment.Length))
                .ToList(),
            Hits = Hits,
            IsActive = IsActive,
            HasExploded = HasExploded,
            SpecificData = GetSpecificData()
        };
    }

    /// <summary>
    /// Override this method to provide component-specific state data
    /// </summary>
    protected virtual ComponentSpecificData? GetSpecificData() => null;
}
