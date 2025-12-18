namespace Sanet.MakaMek.Bots.Data;

public enum PhaseState
{
    Early, // > 70% units remaining
    Mid,   // 30-70% units remaining
    Late   // < 30% units remaining
}