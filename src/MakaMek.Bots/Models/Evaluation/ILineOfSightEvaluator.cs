using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Bots.Models.Evaluation;

/// <summary>
/// Answers line-of-sight questions for tactical evaluation.
/// </summary>
public interface ILineOfSightEvaluator
{
    /// <summary>
    /// Checks whether <paramref name="origin"/> can see <paramref name="target"/>.
    /// </summary>
    bool HasLineOfSight(HexPosition origin, HexPosition target, int originHeight, int targetHeight);
}
