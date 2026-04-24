namespace Sanet.MakaMek.Bots.Utilities;

/// <summary>
/// Utility for processing collections in chunks with cooperative yielding
/// to avoid blocking the main thread, especially important for WASM/browser environments.
/// </summary>
public static class AsyncChunkProcessor
{
    /// <summary>
    /// Processes items in chunks using an asynchronous processor function,
    /// yielding between chunks for cooperative scheduling.
    /// </summary>
    public static async Task<List<TResult>> ProcessInChunksAsync<TItem, TResult>(
        IReadOnlyList<TItem> items,
        Func<TItem, Task<TResult>> processor,
        int chunkSize = 15,
        CancellationToken cancellationToken = default)
    {
        var results = new List<TResult>(items.Count);

        for (var startIndex = 0; startIndex < items.Count; startIndex += chunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var endIndex = Math.Min(startIndex + chunkSize, items.Count);
            for (var i = startIndex; i < endIndex; i++)
            {
                results.Add(await processor(items[i]));
            }

            await Task.Yield();
        }

        return results;
    }
}
