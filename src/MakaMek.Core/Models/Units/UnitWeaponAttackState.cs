using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Models.Units;

/// <summary>
/// Represents the weapon attack state for a unit, tracking weapon selection and targeting information
/// </summary>
public class UnitWeaponAttackState
{
    private readonly Dictionary<Weapon, Unit> _weaponTargets = new();
    
    /// <summary>
    /// Gets the primary target based on current weapon selections
    /// </summary>
    public Unit? PrimaryTarget { get; private set; }
    
    /// <summary>
    /// Gets the arm location that is committed when the unit is prone (null if no arm is committed)
    /// </summary>
    public PartLocation? CommittedArmLocation { get; private set; }
    
    /// <summary>
    /// Gets a read-only dictionary of weapon-to-target mappings
    /// </summary>
    public IReadOnlyDictionary<Weapon, Unit> WeaponTargets => _weaponTargets;
    
    /// <summary>
    /// Gets all unique targets that have weapons assigned to them
    /// </summary>
    public IEnumerable<Unit> AllTargets => _weaponTargets.Values.Distinct();
    
    /// <summary>
    /// Gets all weapons that are currently selected to fire
    /// </summary>
    public IEnumerable<Weapon> SelectedWeapons => _weaponTargets.Keys;
    
    /// <summary>
    /// Adds or updates a weapon target assignment
    /// </summary>
    /// <param name="weapon">The weapon to assign</param>
    /// <param name="target">The target for the weapon</param>
    /// <param name="attacker">The attacking unit (used for primary target calculation)</param>
    public void SetWeaponTarget(Weapon weapon, Unit target, Unit attacker)
    {
        _weaponTargets[weapon] = target;
        UpdateCommittedArm(attacker);
        UpdatePrimaryTarget(attacker);
    }
    
    /// <summary>
    /// Removes a weapon from the target assignments
    /// </summary>
    /// <param name="weapon">The weapon to remove</param>
    /// <param name="attacker">The attacking unit (used for primary target calculation)</param>
    public void RemoveWeaponTarget(Weapon weapon, Unit attacker)
    {
        _weaponTargets.Remove(weapon);
        UpdateCommittedArm(attacker);
        UpdatePrimaryTarget(attacker);
    }
    
    /// <summary>
    /// Clears all weapon target assignments
    /// </summary>
    public void ClearAllWeaponTargets()
    {
        _weaponTargets.Clear();
        CommittedArmLocation = null;
        PrimaryTarget = null;
    }
    
    /// <summary>
    /// Checks if a specific weapon is assigned to a target
    /// </summary>
    /// <param name="weapon">The weapon to check</param>
    /// <param name="target">The target to check against (optional)</param>
    /// <returns>True if the weapon is assigned to the specified target (or any target if target is null)</returns>
    public bool IsWeaponAssigned(Weapon weapon, Unit? target = null)
    {
        if (!_weaponTargets.TryGetValue(weapon, out var assignedTarget))
            return false;
            
        return target == null || assignedTarget == target;
    }

    /// <summary>
    /// Updates the committed arm location based on current weapon selections when the unit is prone
    /// </summary>
    private void UpdateCommittedArm(Unit attacker)
    {
        // Only track committed arm if the unit is prone
        if (attacker is not Mech { IsProne: true })
        {
            CommittedArmLocation = null;
            return;
        }

        // Find the first arm location among selected weapons
        var firstArmLoc = _weaponTargets.Keys
            .Select(w => w.FirstMountPartLocation)
            .FirstOrDefault(loc => loc.HasValue && loc.Value.IsArm());
        CommittedArmLocation = firstArmLoc;
    }

    /// <summary>
    /// Manually sets the primary target
    /// </summary>
    /// <param name="target">The target to set as primary</param>
    public void SetPrimaryTarget(Unit? target)
    {
        if (target == null)
        {
            PrimaryTarget = null;
            return;
        }
        // Verify that the target is actually in our target list
        if (!AllTargets.Contains(target))
        {
            return;
        }

        PrimaryTarget = target;
    }

    /// <summary>
    /// Updates the primary target based on current weapon selections
    /// </summary>
    private void UpdatePrimaryTarget(Unit attacker)
    {
        if (_weaponTargets.Count == 0)
        {
            PrimaryTarget = null;
            return;
        }

        // Get all unique targets
        var targets = _weaponTargets.Values.Distinct().ToList();

        // If only one target, it's the primary
        if (targets.Count == 1)
        {
            PrimaryTarget = targets[0];
            return;
        }

        // If primary target is already set and still valid, keep it
        if (PrimaryTarget != null && targets.Contains(PrimaryTarget))
        {
            return;
        }

        // For multiple targets, prefer targets in the forward arc
        if (attacker.Position == null)
        {
            PrimaryTarget = targets[0];
            return;
        }

        var attackerPosition = attacker.Position;
        var facing = attacker is Mech mech ? mech.TorsoDirection : attackerPosition.Facing;

        if (facing == null)
        {
            PrimaryTarget = targets[0];
            return;
        }

        // Find targets in the forward arc
        var targetsInForwardArc = targets
            .Where(t => t.Position != null &&
                       attackerPosition.Coordinates.IsInFiringArc(
                           t.Position.Coordinates,
                           facing.Value,
                           FiringArc.Front))
            .ToList();

        PrimaryTarget = targetsInForwardArc.Count > 0 ? targetsInForwardArc[0] : targets[0];
    }
}
