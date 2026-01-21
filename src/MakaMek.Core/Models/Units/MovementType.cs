namespace Sanet.MakaMek.Core.Models.Units;

public enum MovementType
{
    StandingStill = 0, // No movement
    Walk = 1,   // Base movement
    Run = 2,    // 1.5x walking
    Jump = 3,   // Requires jump jets
    Prone = 4
}
