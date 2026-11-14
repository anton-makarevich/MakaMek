namespace Sanet.MakaMek.Map.Pathfinding;

/// <summary>
/// Breadth-first exploration of a lazily generated graph. Records the cheapest cost found for
/// every discovered node and re-visits a node only when a cheaper route reaches it. Shared by the
/// walking and jumping reachability traversals, which differ only in their successor rules.
/// </summary>
internal static class HexBreadthFirstSearch
{
    /// <summary>
    /// Explores the graph from <paramref name="start"/>.
    /// </summary>
    /// <param name="start">Node the search starts from.</param>
    /// <param name="expand">
    /// Produces the successors of a node together with their total cost. Receives the node and the
    /// best cost recorded for it so far.
    /// </param>
    /// <returns>Minimum cost recorded for every node reached, including the start at zero.</returns>
    public static Dictionary<TNode, int> Run<TNode>(
        TNode start,
        Func<TNode, int, IEnumerable<(TNode Node, int Cost)>> expand)
        where TNode : notnull
    {
        var costs = new Dictionary<TNode, int> { [start] = 0 };
        var toVisit = new Queue<TNode>();
        toVisit.Enqueue(start);

        while (toVisit.Count > 0)
        {
            var current = toVisit.Dequeue();
            var currentCost = costs[current];

            foreach (var (node, cost) in expand(current, currentCost))
            {
                if (costs.TryGetValue(node, out var knownCost) && knownCost <= cost)
                    continue;

                costs[node] = cost;
                toVisit.Enqueue(node);
            }
        }

        return costs;
    }
}
