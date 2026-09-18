using Sanet.MakaMek.Core.Exceptions;
using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components;

public abstract class Component : IManufacturedItem
{
    private readonly List<CriticalSlotAssignment> _slotAssignments = [];
    protected readonly ComponentDefinition _definition;

    protected Component(ComponentDefinition definition, ComponentData? componentData = null)
    {
        _definition = definition;
        Name = definition.Name;

        // Restore mutable state if provided
        if (componentData == null) return;
        if (!string.IsNullOrEmpty(componentData.Name)) Name = componentData.Name;
        Manufacturer = componentData.Manufacturer;
        Hits = componentData.Hits;
        IsActive = componentData.IsActive;
        HasExploded = componentData.HasExploded;
        IsFlooded = componentData.IsFlooded;
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

    public bool IsFlooded { get; private set; }

    public void Flood()
    {
        IsFlooded = true;
    }

    public virtual bool IsAvailable => IsActive
                               && !IsDestroyed
                               && IsMounted
                               && !IsFlooded
                               && !SlotAssignments.Any(a => a.UnitPart.IsDestroyed);

    /// <summary>
    /// Indicates whether this component is submerged in water.
    /// A component is submerged when any of its mounted parts are submerged.
    /// </summary>
    public bool IsSubmerged => MountedOn.Any(part => part.IsSubmerged);

    public int Size => _definition.Size;
    public string Manufacturer => field ?? "Unknown";
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
        ArgumentNullException.ThrowIfNull(mountLocation);
        ArgumentNullException.ThrowIfNull(slots);

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

        var assignments = new List<CriticalSlotAssignment>();
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
                // Break in sequence; stage the current range for an atomic commit.
                assignments.Add(new CriticalSlotAssignment
                {
                    UnitPart = mountLocation,
                    FirstSlot = start,
                    Length = length
                });

                // Start a new rangeBracket
                start = slots[i];
                length = 1;
            }
        }

        assignments.Add(new CriticalSlotAssignment
        {
            UnitPart = mountLocation,
            FirstSlot = start,
            Length = length
        });

        MountAllOrThrow(assignments);
    }

    private void MountAllOrThrow(IEnumerable<CriticalSlotAssignment> assignments)
    {
        ArgumentNullException.ThrowIfNull(assignments);
        if (IsMounted) return;

        var staged = assignments.ToList();
        if (staged.Count == 0) return;
        if (staged.Any(a => a is null))
            throw new ArgumentException("Assignments cannot contain null entries.", nameof(assignments));

        foreach (var assignment in staged)
        {
            if (assignment.UnitPart is null)
                throw new ArgumentException("Assignment.UnitPart cannot be null.", nameof(assignments));
            if (assignment.FirstSlot < 0 || assignment.Length <= 0)
                throw new ArgumentOutOfRangeException(nameof(assignments), "FirstSlot must be >= 0 and Length > 0.");
            if (assignment.FirstSlot > assignment.UnitPart.TotalSlots - assignment.Length)
                throw new ComponentException("Slot assignment exceeds available slots of the unit part.");
        }

        foreach (var group in staged.GroupBy(a => a.UnitPart))
        {
            var intervals = new List<(int Start, int End)>();
            foreach (var assignment in group)
            {
                var start = assignment.FirstSlot;
                var end = start + assignment.Length - 1;
                if (intervals.Any(interval => !(end < interval.Start || start > interval.End)))
                    throw new ComponentException("Overlapping slot assignments for the same unit part.");
                intervals.Add((start, end));
            }
        }

        foreach (var assignment in staged)
        {
            var start = assignment.FirstSlot;
            var end = start + assignment.Length - 1;
            if (_slotAssignments.Any(existing =>
                    existing.UnitPart == assignment.UnitPart &&
                    !(end < existing.FirstSlot || start > existing.FirstSlot + existing.Length - 1)))
                throw new ComponentException("Assignment overlaps existing mounts on the same part.");
        }

        var totalUniqueSlots = _slotAssignments.SelectMany(assignment => assignment.Slots)
            .Concat(staged.SelectMany(assignment => assignment.Slots))
            .Distinct()
            .Count();
        if (totalUniqueSlots > Size)
            throw new ComponentException($"Component {Name} requires {Size} slots.");

        _slotAssignments.AddRange(staged);
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
        if (!CanExplode || HasExploded) return;
        HasExploded = true;
        FirstMountPart?.Unit?.Pilot?.ExplosionHit();
    }

    public int HealthPoints => _definition.HealthPoints;
    public int Hits { get; private set; }
    public bool IsDestroyed => Hits >= HealthPoints;

    public decimal Mass => _definition.Mass;

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
            IsFlooded = IsFlooded,
            SpecificData = GetSpecificData()
        };
    }

    /// <summary>
    /// Override this method to provide component-specific state data
    /// </summary>
    protected virtual ComponentSpecificData? GetSpecificData() => null;
}
