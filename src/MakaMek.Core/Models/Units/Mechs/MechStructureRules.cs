using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Core.Models.Units.Mechs;

/// <summary>
/// The 'Mech structural rules: which destroyed parts destroy the mech, which arms it blows off,
/// where damage transfers to, and whether it is immobile. Extracted from <see cref="Mech"/>.
/// </summary>
internal static class MechStructureRules
{
    public static bool IsPilotLost(Mech mech) => mech.Pilot?.IsDead == true;

    public static bool IsHeadDestroyed(Mech mech) =>
        mech.Parts.TryGetValue(PartLocation.Head, out var head) && head.IsDestroyed;

    /// <summary>
    /// A mech can fire only while its sensors are available (they are unavailable after two
    /// critical hits).
    /// </summary>
    public static bool AreSensorsAvailable(Mech mech) =>
        mech.GetAllComponents<Sensors>().FirstOrDefault()?.IsAvailable == true;

    public static bool IsCenterTorsoDestroyed(Mech mech) =>
        mech.Parts.TryGetValue(PartLocation.CenterTorso, out var centerTorso) && centerTorso.IsDestroyed;

    /// <summary>
    /// Side torso destruction blows off the corresponding arm.
    /// </summary>
    public static void BlowOffArmOnTorsoDestruction(Mech mech, PartLocation torsoLocation, PartLocation armLocation)
    {
        if (mech.Parts.TryGetValue(torsoLocation, out var torso)
            && torso.CurrentStructure <= 0
            && mech.Parts.TryGetValue(armLocation, out var arm)
            && !arm.IsBlownOff)
        {
            arm.BlowOff();
        }
    }

    public static PartLocation? GetTransferLocation(PartLocation location) => location switch
    {
        PartLocation.LeftArm => PartLocation.LeftTorso,
        PartLocation.RightArm => PartLocation.RightTorso,
        PartLocation.LeftLeg => PartLocation.LeftTorso,
        PartLocation.RightLeg => PartLocation.RightTorso,
        PartLocation.LeftTorso => PartLocation.CenterTorso,
        PartLocation.RightTorso => PartLocation.CenterTorso,
        _ => null
    };

    /// <summary>
    /// Determines if the mech is immobile based on the rules:
    /// </summary>
    public static bool IsImmobile(Mech mech)
    {
        // A mech is immobile if it's shutdown
        if (mech.IsShutdown)
            return true;

        // A mech with an unconscious pilot is immobile
        if (mech.Pilot == null || mech.Pilot.IsConscious == false)
            return true;

        // A mech with both legs and both arms destroyed/blown off is immobile
        var destroyedLegs = mech.Parts.Values.OfType<Leg>().Count(leg => leg.IsDestroyed);
        if (destroyedLegs < 2) return false;
        var destroyedArms = mech.Parts.Values.OfType<Arm>().Count(arm => arm.IsDestroyed);
        if (destroyedArms >= 2)
            return true;

        return false;
    }

    /// <summary>
    /// The mech's battle value: base tonnage value plus the battle value of its weapons.
    /// </summary>
    public static int CalculateBattleValue(Mech mech)
    {
        var bv = mech.Tonnage * 100; // Base value
        bv += mech.GetAllComponents<Weapon>().Sum(w => w.BattleValue);
        return bv;
    }
}
