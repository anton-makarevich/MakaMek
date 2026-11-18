using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

/// <summary>
/// One declared weapon attack in resolution order: who fires which weapon at which unit.
/// </summary>
internal readonly record struct WeaponAttackQueueItem(
    IPlayer Player,
    IUnit Attacker,
    Weapon? Weapon,
    IUnit? TargetUnit,
    WeaponTargetData WeaponTargetData,
    LocationSlotAssignment PrimaryAssignment);

/// <summary>
/// Builds the weapon attack resolution queue from the declarations of the players in initiative
/// order. Extracted from <see cref="WeaponAttackResolutionPhase"/>.
/// </summary>
internal sealed class WeaponAttackQueueBuilder(ServerGame game)
{
    public List<WeaponAttackQueueItem> Build(IReadOnlyList<IPlayer> playersInOrder)
    {
        var queue = new List<WeaponAttackQueueItem>();
        var allUnits = game.Players.SelectMany(p => p.Units).ToList();

        foreach (var (player, unit) in GetUnitsWithTargets(playersInOrder))
            AppendUnitAttacks(queue, player, unit, allUnits);

        return queue;
    }

    /// <summary>
    /// The units of each player that can still fire and declared an attack, in initiative order.
    /// </summary>
    private static IEnumerable<(IPlayer Player, IUnit Unit)> GetUnitsWithTargets(
        IEnumerable<IPlayer> playersInOrder) =>
        playersInOrder.SelectMany(player => player.Units
            .Where(unit => unit is { CanFireWeapons: true, HasDeclaredWeaponAttack: true })
            .Select(unit => (player, unit)));

    private static void AppendUnitAttacks(
        List<WeaponAttackQueueItem> queue,
        IPlayer player,
        IUnit unit,
        List<IUnit> allUnits)
    {
        foreach (var weaponTarget in unit.DeclaredWeaponTargets ?? [])
        {
            var primaryAssignment = weaponTarget.Weapon.Assignments[0];
            var weapon = unit.GetMountedComponentAtLocation<Weapon>(
                primaryAssignment.Location, primaryAssignment.FirstSlot);
            var targetUnit = allUnits.FirstOrDefault(u => u.Id == weaponTarget.TargetId);
            queue.Add(new WeaponAttackQueueItem(player, unit, weapon, targetUnit, weaponTarget, primaryAssignment));
        }
    }
}
