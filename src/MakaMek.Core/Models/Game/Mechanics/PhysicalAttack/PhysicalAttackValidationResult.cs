namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// Describes whether a physical-attack declaration can be accepted by the server.
/// </summary>
public readonly record struct PhysicalAttackValidationResult(bool IsValid, string? Error)
{
    /// <summary>Creates a successful validation result.</summary>
    public static PhysicalAttackValidationResult Valid() => new(true, null);

    /// <summary>Creates a rejected validation result with a diagnostic reason.</summary>
    public static PhysicalAttackValidationResult Invalid(string error) => new(false, error);
}
