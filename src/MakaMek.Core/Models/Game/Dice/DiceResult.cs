namespace Sanet.MakaMek.Core.Models.Game.Dice;

public record DiceResult
{
    private int _result;

    public DiceResult(int result)
    {
        Result = result;
    }

    public int Result
    {
        get => _result;
        private set
        {
            if (value is < 1 or > 6)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Result must be between 1 and 6.");
            }
            _result = value;
        }
    }
}