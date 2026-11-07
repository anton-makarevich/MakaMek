using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// The outcome of validating a Charge or DFA declaration carried by a movement command: either the
/// declaration the server should store, with the distance it computed, or the error to send back.
/// </summary>
public sealed record MovementAttackDeclarationValidation
{
    private MovementAttackDeclarationValidation()
    {
    }

    /// <summary>
    /// The error that invalidates the command, or null when it is valid.
    /// </summary>
    public ErrorCode? ErrorCode { get; private init; }

    /// <summary>
    /// The declaration to store, with the server-computed distance. Null when the command is invalid.
    /// </summary>
    public PhysicalAttackDeclarationData? Declaration { get; private init; }

    public bool IsValid => ErrorCode is null;

    public static MovementAttackDeclarationValidation Invalid(ErrorCode errorCode) =>
        new() { ErrorCode = errorCode };

    public static MovementAttackDeclarationValidation Valid(PhysicalAttackDeclarationData declaration) =>
        new() { Declaration = declaration };
}
