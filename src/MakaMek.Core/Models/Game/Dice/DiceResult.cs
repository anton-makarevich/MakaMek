namespace Sanet.MakaMek.Core.Models.Game.Dice;

public record DiceResult(int Result)
{
    public int Result { get; init; } = Result is >= 1 and <= 6
        ? Result
        : throw new ArgumentOutOfRangeException(nameof(Result), "Result must be between 1 and 6.");
}