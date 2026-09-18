namespace Sanet.MakaMek.Core.Data.Game.Mechanics;

/// <summary>Identifies why a unit was moved without a normal movement command.</summary>
public enum DisplacementReason
{
    /// <summary>Movement caused by a terrain or bridge domino effect.</summary>
    DominoEffect,
    /// <summary>Movement caused by a successful physical Push.</summary>
    PhysicalAttackPush
}
