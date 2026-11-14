using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

/// <summary>
/// The damage a unit accumulated during the weapon attack resolution phase, used to run its
/// piloting skill rolls once the phase ends instead of after every attack.
/// </summary>
internal sealed class WeaponAttackAccumulatedDamage
{
    public List<ComponentHitData> AllComponentHits { get; } = [];
    public List<PartLocation> AllDestroyedParts { get; } = [];
}

/// <summary>
/// Shared phase state between the attack applier and the end-of-phase PSR calculator.
/// </summary>
internal sealed class WeaponAttackResolutionState
{
    private readonly Dictionary<Guid, WeaponAttackAccumulatedDamage> _accumulatedDamage = new();

    public IReadOnlyDictionary<Guid, WeaponAttackAccumulatedDamage> AccumulatedDamage => _accumulatedDamage;

    public void Clear() => _accumulatedDamage.Clear();

    public void Accumulate(Guid unitId, IEnumerable<ComponentHitData> componentHits, IEnumerable<PartLocation> destroyedParts)
    {
        if (!_accumulatedDamage.TryGetValue(unitId, out var accumulated))
        {
            accumulated = new WeaponAttackAccumulatedDamage();
            _accumulatedDamage[unitId] = accumulated;
        }

        accumulated.AllComponentHits.AddRange(componentHits);
        foreach (var part in destroyedParts)
            accumulated.AllDestroyedParts.Add(part);
    }
}
