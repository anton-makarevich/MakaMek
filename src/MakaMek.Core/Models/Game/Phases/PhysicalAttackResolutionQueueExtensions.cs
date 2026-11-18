namespace Sanet.MakaMek.Core.Models.Game.Phases;

/// <summary>
/// Queue-level helpers for the physical-attack resolution phase.
/// </summary>
internal static class PhysicalAttackResolutionQueueExtensions
{
    extension(IReadOnlyList<ResolutionQueueItem> queue)
    {
        /// <summary>
        /// The pushes in the queue whose attacker and target are swapped by another queued push.
        /// Only attacks that made it through the gates count, so an invalid push never pairs.
        /// </summary>
        public HashSet<(Guid AttackerId, Guid TargetId)> GetReciprocalPushPairs()
        {
            var pushKeys = queue
                .Where(item => item.Attack?.Context.AttackType == PhysicalAttackType.Push)
                .Select(item => (
                    AttackerId: item.Attack!.Value.Context.Attacker.Id,
                    TargetId: item.Attack.Value.Context.Target.Id))
                .ToHashSet();

            var reciprocal = new HashSet<(Guid AttackerId, Guid TargetId)>();
            foreach (var key in pushKeys)
            {
                if (pushKeys.Contains((key.TargetId, key.AttackerId)))
                    reciprocal.Add(key);
            }

            return reciprocal;
        }
    }
}
