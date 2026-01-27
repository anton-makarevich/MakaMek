using Sanet.MakaMek.Core.Data.Game.Commands.Server;

namespace Sanet.MakaMek.Core.Models.Game;

public readonly record struct CommandValidationResult(bool IsValid, ErrorCode? ErrorCode = null)
{
    public static CommandValidationResult Valid() => new(true);

    public static CommandValidationResult Invalid(ErrorCode errorCode) => new(false, errorCode);
}
