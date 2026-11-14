using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Units.Mechs;

/// <summary>
/// Torso direction and rotation for a 'Mech: the current torso facing, whether it has been
/// twisted, and the rotation itself. Extracted from <see cref="Mech"/> so the mech only exposes
/// the values and this class owns the part knowledge.
/// </summary>
internal static class MechTorsoController
{
    public static HexDirection? GetTorsoDirection(Mech mech) =>
        mech.Parts.Values.OfType<Torso>().FirstOrDefault()?.Facing;

    public static bool HasUsedTorsoTwist(Mech mech)
    {
        if (mech.Position == null) return false;
        var torsos = mech.Parts.Values.OfType<Torso>();
        return torsos.Any(t => t.Facing != mech.Position.Facing);
    }

    /// <summary>
    /// Rotates the torso towards the given facing when it is within the allowed rotation range.
    /// </summary>
    public static void RotateTorso(Mech mech, HexDirection newFacing)
    {
        if (!mech.CanRotateTorso)
            return;

        var currentUnitFacing = (int)mech.Position!.Facing;
        var newFacingInt = (int)newFacing;

        // Calculate steps in both directions (clockwise and counterclockwise)
        var clockwiseSteps = (newFacingInt - currentUnitFacing + 6) % 6;
        var counterClockwiseSteps = (currentUnitFacing - newFacingInt + 6) % 6;

        // Use the smaller number of steps
        var steps = Math.Min(clockwiseSteps, counterClockwiseSteps);

        // Check if rotation is within the allowed rangeBracket
        if (steps > mech.PossibleTorsoRotation) return;
        foreach (var torso in mech.Parts.Values.OfType<Torso>())
        {
            torso.Rotate(newFacing);
        }
    }

    /// <summary>
    /// Resets the rotation of every torso back to the unit facing.
    /// </summary>
    public static void ResetTorsos(Mech mech)
    {
        foreach (var torso in mech.Parts.Values.OfType<Torso>())
        {
            torso.ResetRotation();
        }
    }
}
