using System;
using System.Collections.Generic;
using System.Linq;
using Sanet.MakaMek.Core.Events;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Presentation.UiStates;

namespace Sanet.MakaMek.Avalonia.Utils;

internal class UnitState : IEquatable<UnitState>
{
    public HexPosition? Position { get; init; }
    public bool IsDeployed { get; init; }
    public object? SelectedUnit { get; init; }
    public IEnumerable<StateAction> Actions { get; init; } = [];
    public bool IsWeaponsPhase { get; init; }
    public HexDirection? TorsoDirection { get; init; }
    public int TotalMaxArmor { get; init; }
    public int TotalCurrentArmor { get; init; }
    public int TotalMaxStructure { get; init; }
    public int TotalCurrentStructure { get; init; }
    public IReadOnlyCollection<UiEvent> Events { get; init; } = [];
    public UnitStatus Status { get; init; }

    public bool Equals(UnitState? other)
    {
        if (other == null) return false;

        return ArePositionsEqual(Position, other.Position) &&
               IsDeployed == other.IsDeployed &&
               SelectedUnit == other.SelectedUnit &&
               AreActionsEqual(Actions, other.Actions) &&
               IsWeaponsPhase == other.IsWeaponsPhase &&
               TorsoDirection == other.TorsoDirection &&
               TotalMaxArmor == other.TotalMaxArmor &&
               TotalCurrentArmor == other.TotalCurrentArmor &&
               TotalMaxStructure == other.TotalMaxStructure &&
               TotalCurrentStructure == other.TotalCurrentStructure &&
               Status == other.Status &&
               AreEventsEqual(Events, other.Events);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as UnitState);
    }

    public override int GetHashCode()
    {
        // Simple implementation that considers the main properties
        return HashCode.Combine(
            Position?.GetHashCode() ?? 0,
            IsDeployed.GetHashCode(),
            SelectedUnit?.GetHashCode() ?? 0,
            IsWeaponsPhase.GetHashCode(),
            TorsoDirection?.GetHashCode() ?? 0,
            Status,
            TotalCurrentArmor,
            TotalCurrentStructure
        );
    }

    private bool ArePositionsEqual(HexPosition? pos1, HexPosition? pos2)
    {
        if (pos1 == null && pos2 == null) return true;
        if (pos1 == null || pos2 == null) return false;
        return Math.Abs(pos1.Coordinates.H - pos2.Coordinates.H) < 0.01 &&
               Math.Abs(pos1.Coordinates.V - pos2.Coordinates.V) < 0.01 &&
               pos1.Facing == pos2.Facing;
    }

    private static bool AreActionsEqual(IEnumerable<StateAction> actions1, IEnumerable<StateAction> actions2)
    {
        var list1 = actions1.ToList();
        var list2 = actions2.ToList();

        if (list1.Count != list2.Count) return false;
        for(var i = 0; i < list1.Count; i++)
        {
            if (!list1[i].Equals(list2[i])) return false;
        }
        return true;
    }

    private bool AreEventsEqual(IEnumerable<UiEvent> events1, IEnumerable<UiEvent> events2)
    {
        // Only check if there are any events, since we're just interested in 
        // whether there are new events to process
        return !events1.Any() && !events2.Any();
    }
}