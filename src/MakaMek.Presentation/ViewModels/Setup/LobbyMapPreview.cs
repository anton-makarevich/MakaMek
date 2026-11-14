using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Presentation.ViewModels.Setup;

/// <summary>
/// Renders and owns the lobby map preview shown on the join screen, including cancellation and image
/// disposal of superseded renders. Kept out of the join controller so the controller only forwards the
/// preview properties.
/// </summary>
public sealed class LobbyMapPreview : IDisposable
{
    private readonly IMapPreviewRenderer _previewRenderer;
    private readonly ILogger _logger;
    private CancellationTokenSource? _previewCts;
    private object? _previewImage;

    public LobbyMapPreview(IMapPreviewRenderer previewRenderer, ILogger logger)
    {
        _previewRenderer = previewRenderer;
        _logger = logger;
    }

    public BattleMap? Map { get; private set; }

    public object? Image => _previewImage;

    public bool HasMap => Map != null;

    /// <summary>
    /// Applies a new lobby map and renders its preview. Returns the property names whose bindings
    /// changed so the caller can re-raise them.
    /// </summary>
    public async Task UpdateAsync(BattleMap? map)
    {
        Map = map;

        _previewCts?.Cancel();
        _previewCts?.Dispose();

        // Drop the previous preview image immediately: a null replacement, a cancelled
        // render, or a render that returns null must never leave a stale map's image on
        // screen.
        (_previewImage as IDisposable)?.Dispose();
        _previewImage = null;

        if (map == null)
        {
            _previewCts = null;
            return;
        }

        _previewCts = new CancellationTokenSource();
        var token = _previewCts.Token;

        try
        {
            var newPreview = await _previewRenderer.GeneratePreview(map, cancellationToken: token);
            if (!token.IsCancellationRequested && newPreview != null)
            {
                _previewImage = newPreview;
            }
            else
            {
                // Render was superseded or produced nothing; dispose the unused result.
                (newPreview as IDisposable)?.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Map preview generation cancelled - a newer map superseded this preview");
        }
    }

    public void Dispose()
    {
        _previewCts?.Cancel();
        _previewCts?.Dispose();
        _previewCts = null;
        (_previewImage as IDisposable)?.Dispose();
        _previewImage = null;
    }
}
