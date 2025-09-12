namespace Sanet.MakaMek.Core.Data.Units;

/// <summary>
/// Represents the critical slot layout for a specific location
/// </summary>
public class LocationSlotLayout
{
    private readonly Dictionary<int, MakaMekComponent> _slotAssignments = new();

    /// <summary>
    /// Gets all component slot assignments for this location
    /// </summary>
    public IReadOnlyList<ComponentSlotAssignment> ComponentAssignments
    {
        get
        {
            var assignments = new List<ComponentSlotAssignment>();
            var processedSlots = new HashSet<int>();

            foreach (var (slot, component) in _slotAssignments.OrderBy(kvp => kvp.Key))
            {
                if (processedSlots.Contains(slot))
                    continue;

                // Find all consecutive slots with the same component
                var componentSlots = new List<int> { slot };
                processedSlots.Add(slot);

                // Check for multi-slot components
                for (int nextSlot = slot + 1; nextSlot < 12; nextSlot++)
                {
                    if (_slotAssignments.TryGetValue(nextSlot, out var nextComponent) &&
                        nextComponent == component)
                    {
                        componentSlots.Add(nextSlot);
                        processedSlots.Add(nextSlot);
                    }
                    else
                    {
                        break;
                    }
                }

                assignments.Add(new ComponentSlotAssignment
                {
                    Component = component,
                    Slots = componentSlots.ToArray()
                });
            }

            return assignments;
        }
    }

    /// <summary>
    /// Gets the component at a specific slot, or null if empty
    /// </summary>
    /// <param name="slot">The slot index (0-based)</param>
    /// <returns>The component at the slot, or null if empty</returns>
    public MakaMekComponent? GetComponentAtSlot(int slot)
    {
        return _slotAssignments.TryGetValue(slot, out var component) ? component : null;
    }

    /// <summary>
    /// Assigns a component to a specific slot
    /// </summary>
    /// <param name="slot">The slot index (0-based)</param>
    /// <param name="component">The component to assign</param>
    public void AssignComponent(int slot, MakaMekComponent component)
    {
        _slotAssignments[slot] = component;
    }

    /// <summary>
    /// Gets all occupied slot indices
    /// </summary>
    public IEnumerable<int> OccupiedSlots => _slotAssignments.Keys;

    /// <summary>
    /// Gets the total number of occupied slots
    /// </summary>
    public int OccupiedSlotCount => _slotAssignments.Count;
}