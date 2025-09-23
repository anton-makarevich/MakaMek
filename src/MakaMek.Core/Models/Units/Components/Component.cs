using Sanet.MakaMek.Core.Exceptions;
using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components;

public abstract class Component : IManufacturedItem
{
    private readonly List<CriticalSlotAssignment> _slotAssignments = [];
    protected readonly ComponentDefinition _definition;
    private readonly string? _manufacturerOverride;

    protected Component(ComponentDefinition definition, ComponentData? componentData = null)
    {
        _definition = definition;
        Name = definition.Name;

        // Restore mutable state if provided
        if (componentData == null) return;
        if (!string.IsNullOrEmpty(componentData.Name)) Name = componentData.Name;
        _manufacturerOverride = componentData.Manufacturer;
        Hits = componentData.Hits;
        IsActive = componentData.IsActive;
        HasExploded = componentData.HasExploded;
    }

    public string Name { get; protected set; }
    
    public int[] MountedAtFirstLocationSlots => FirstMountPartLocation.HasValue 
        ? GetMountedAtLocationSlots(FirstMountPartLocation.Value)
        : [];
    
    public int[] GetMountedAtLocationSlots(PartLocation location) => SlotAssignments
        .Where(a => a.Location == location)
        .SelectMany(a => a.Slots)
        .OrderBy(slot => slot)
        .ToArray(); 

    public bool IsActive { get; private set; } = true;

    public bool IsAvailable => IsActive
                               && !IsDestroyed
                               && IsMounted
                               && !SlotAssignments.Any(a => a.UnitPart.IsDestroyed);

    public int Size => _definition.Size;
    public string Manufacturer => _manufacturerOverride ?? "Unknown";
    public int BattleValue => _definition.BattleValue;
    public bool IsRemovable => _definition.IsRemovable;

    // Multi-location slot assignments
    public IReadOnlyList<CriticalSlotAssignment> SlotAssignments => _slotAssignments.AsReadOnly();

    // Multi-location mounted parts
    public IReadOnlyList<UnitPart> MountedOn => SlotAssignments.Select(a => a.UnitPart)
        .Distinct().ToList();

    // Component type property for mapping to MakaMekComponent enum
    public MakaMekComponent ComponentType => _definition.ComponentType;

    // component is mounted when all required slots are assigned
    public bool IsMounted => SlotAssignments.Sum(a => a.Length) == Size && SlotAssignments.Count > 0;

    public void Mount(UnitPart mountLocation, int[] slots)
    {
        if (slots.Length > Size)
        {
            throw new ComponentException($"Component {Name} requires {Size} slots.");
        }
        if (slots.Length == 0)
            return;

        Array.Sort(slots); // Ensure slots are ordered

        for (var i = 1; i < slots.Length; i++)
        {
            if (slots[i] == slots[i - 1])
            {
                throw new ComponentException("Slot assignments cannot contain duplicates.");
            }
        }

        var start = slots[0];
        var length = 1;

        for (var i = 1; i < slots.Length; i++)
        {
            if (slots[i] == slots[i - 1] + 1)
            {
                // Still consecutive
                length++;
            }
            else
            {
                // Break in sequence → commit current range
                Mount(new CriticalSlotAssignment
                {
                    UnitPart = mountLocation,
                    FirstSlot = start,
                    Length = length
                });

                // Start a new range
                start = slots[i];
                length = 1;
            }
        }

        // Commit the last range
        Mount(new CriticalSlotAssignment
        {
            UnitPart = mountLocation,
            FirstSlot = start,
            Length = length
        });
    }
    
    private void Mount(CriticalSlotAssignment slotAssignment)
    {
        if (IsMounted) return;
        if (slotAssignment.FirstSlot < 0 || slotAssignment.Length <= 0)
            throw new ArgumentOutOfRangeException(nameof(slotAssignment));
        if (slotAssignment.FirstSlot + slotAssignment.Length > slotAssignment.UnitPart.TotalSlots)
            throw new ComponentException("Slot assignment exceeds available slots of the unit part.");
        var occupiedSlots = _slotAssignments.Sum(a => a.Length);
        if (occupiedSlots + slotAssignment.Length > Size)
        {
            throw new ComponentException($"Component {Name} requires {Size} slots.");
        }

        _slotAssignments.Add(slotAssignment);
    }

    public void UnMount()
    {
        if (_slotAssignments.Count == 0) return;
        
        if (!IsRemovable) throw new ComponentException($"{Name} is not removable");

        _slotAssignments.Clear();
    }

    public virtual void Hit()
    {
        Hits++;
        if (CanExplode && !HasExploded)
        {
            HasExploded = true;
            FirstMountPart?.Unit?.Pilot?.ExplosionHit();
        }
    }

    public int HealthPoints => _definition.HealthPoints;
    public int Hits { get; private set; }
    public bool IsDestroyed => Hits >= HealthPoints;

    public virtual void Activate() => IsActive = true;
    public virtual void Deactivate() => IsActive = false;

    // Helper methods for multi-location components
    public IEnumerable<PartLocation> GetLocations() => SlotAssignments.Select(a => a.Location)
        .Distinct();

    // Backward compatibility methods
    public UnitPart? FirstMountPart => SlotAssignments.FirstOrDefault()?.UnitPart;
    public PartLocation? FirstMountPartLocation => FirstMountPart?.Location;

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
            Name = Name,
            Manufacturer = Manufacturer,
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
