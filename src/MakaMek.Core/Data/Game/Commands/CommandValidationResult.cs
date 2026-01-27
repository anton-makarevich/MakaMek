namespace Sanet.MakaMek.Core.Data.Game.Commands;

public readonly record struct CommandValidationResult(bool IsValid, ErrorCode? ErrorCode = null)
{
    public static CommandValidationResult Valid() => new(true);

    public static CommandValidationResult Invalid(ErrorCode errorCode) => new(false, errorCode);
}
