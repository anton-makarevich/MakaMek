namespace Sanet.MakaMek.Core.Models.Units;

public enum MovementType
{
    StandingStill, // No movement
    Walk,   // Base movement
    Run,    // 1.5x walking
    Jump,   // Requires jump jets
    Prone
}
