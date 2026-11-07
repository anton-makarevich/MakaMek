using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Presentation.Models.Movement;

/// <summary>
/// Resolves the segments a movement playback should animate. For a declared Charge or DFA the
/// playback must end where the model stops, not in the target hex, so the display matches
/// <c>Unit.Position</c> without a snap when <c>CompletePlayback</c> restores the authoritative state.
/// </summary>
/// <remarks>
/// The stop position comes from <see cref="MovementPathExtensions.GetStopPosition"/>, the same rule
/// the model applies in <c>BaseGame.OnMoveUnit</c>. When that rule returns no stop position (an
/// ordinary move, an interrupt replacement, a skid, or a truncated command) the input segments are
/// returned unchanged. Otherwise the last translation and any trailing turns in the target hex are
/// dropped. A translation samples its <c>From.Facing</c> until it ends, so if the retained path would
/// finish facing a different way, one zero-cost same-hex turn is appended from the stop position to
/// the stop facing. An empty result means the unit already stands at the stop position (a one-hex
/// Charge, for example) and the timeline is <c>null</c>.
/// </remarks>
public static class MovementPlaybackPath
{
    /// <summary>
    /// Returns the segments to animate for a received <c>MoveUnitCommand</c>.
    /// </summary>
    public static IReadOnlyList<PathSegmentData> Resolve(
        IReadOnlyList<PathSegmentData> segments,
        MovementType movementType,
        PhysicalAttackDeclarationData? declaration)
    {
        if (segments.Count == 0) return segments;

        var path = new MovementPath(segments, movementType);
        var stopPosition = path.GetStopPosition(declaration);
        if (stopPosition is not { } stop) return segments;

        var lastTranslationIndex = path.GetLastTranslationIndex();
        var playback = segments.Take(lastTranslationIndex).ToList();

        if (playback.Count > 0)
        {
            var lastRetained = playback[^1];
            var lastRetainedFrom = new HexPosition(lastRetained.From);
            var isTranslation = lastRetainedFrom.Coordinates != new HexPosition(lastRetained.To).Coordinates;

            if (isTranslation && lastRetainedFrom.Facing != stop.Facing)
            {
                // Finish the playback at the model's stop facing without moving again.
                playback.Add(new PathSegmentData
                {
                    From = new HexPosition(stop.Coordinates, lastRetainedFrom.Facing).ToData(),
                    To = stop.ToData(),
                    Costs = []
                });
            }
        }

        return playback;
    }
}
