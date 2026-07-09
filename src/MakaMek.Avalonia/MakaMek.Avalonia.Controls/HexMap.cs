using System.Reactive.Concurrency;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Avalonia.Controls.Extensions;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.ViewModels;

namespace Sanet.MakaMek.Avalonia.Controls;

/// <summary>
/// A canvas control with built-in pan and zoom functionality for hex-based maps.
/// Owns a HexRenderControl internally for rendering all hex tiles.
/// </summary>
public class HexMap : Canvas
{
    private Point _lastPointerPosition;

    private readonly MatrixTransform _mapTransform = new()
    {
        Matrix = new Matrix(1, 0, 0, 1, 0, 0)
    };

    private readonly MapTransformCalculator _calculator = new();

    private const int SelectionThresholdMilliseconds = 250;
    private const double DragThresholdPixels = 3.0;
    private bool _isManipulating;
    private bool _isZooming;
    private bool _isPressed;
    private CancellationTokenSource? _manipulationTokenSource;
    private Point? _clickPosition;

    private readonly Dictionary<IPointer, Point> _activePointers = new();
    private bool _suppressCapture;

    private HexRenderControl? _hexRenderControl;

    public double MinScale
    {
        get => _calculator.MinScale;
        set => _calculator.MinScale = value;
    }

    public double MaxScale
    {
        get => _calculator.MaxScale;
        set => _calculator.MaxScale = value;
    }

    public double ScaleStep { get; set; } = 0.1;

    public event EventHandler<Point>? ContentClicked;

    public HexMap()
    {
        RenderTransform = _mapTransform;
        RenderTransformOrigin = new RelativePoint(new Point(0, 0), RelativeUnit.Absolute);

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerWheelChanged += OnPointerWheelChanged;
        PointerCaptureLost += OnPointerCaptureLost;
    }

    private Point LocalToParent(Point local) => _calculator.LocalToParent(local);

    private void SyncTransform() => _mapTransform.Matrix = _calculator.Matrix;

    /// <summary>
    /// Sets the hex render data and configuration, creating or updating the internal HexRenderControl.
    /// Must be called after Children.Clear() to ensure the renderer is at the bottom layer.
    /// </summary>
    public void SetHexData(
        IEnumerable<HexRenderData> data,
        HexRenderConfiguration configuration,
        ILogger logger,
        ITerrainAssetService terrainAssetService,
        ILocalizationService? localizationService,
        IScheduler scheduler)
    {
        var renderer = new HexRenderControl(terrainAssetService, localizationService, scheduler);
        renderer.SetHexData(data, configuration);
        Children.Insert(0, renderer);
        _hexRenderControl = renderer;
    }

    /// <summary>
    /// Updates the boundary outlines on the internal HexRenderControl.
    /// </summary>
    public void SetBoundaryOutlines(IReadOnlyDictionary<HexCoordinates, HighlightBoundaryOutline>? outlines)
    {
        _hexRenderControl?.SetBoundaryOutlines(outlines);
    }

    /// <summary>
    /// Updates the render configuration on the internal HexRenderControl.
    /// </summary>
    public void UpdateHexConfiguration(HexRenderConfiguration configuration)
    {
        _hexRenderControl?.UpdateConfiguration(configuration);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_suppressCapture && e.Pointer.Type is PointerType.Touch or PointerType.Pen)
        {
            _suppressCapture = false;
            return;
        }

        if (e.Pointer.Type is PointerType.Touch or PointerType.Pen)
        {
            try { e.Pointer.Capture(this); } catch { /* ignore */ }
        }

        var localPos = e.GetPosition(this);
        var parentPos = LocalToParent(localPos);
        _activePointers[e.Pointer] = parentPos;

        if (_activePointers.Count >= 2 && !_isZooming)
        {
            StartPinch();
            _isZooming = true;
            _isManipulating = true;
            _manipulationTokenSource?.Cancel();
            return;
        }

        if (_activePointers.Count >= 2) return;

        _lastPointerPosition = parentPos;
        _isManipulating = false;

        _manipulationTokenSource?.Cancel();
        _manipulationTokenSource = new CancellationTokenSource();
        var token = _manipulationTokenSource.Token;
        Task.Delay(SelectionThresholdMilliseconds, token)
            .ContinueWith(_ =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (!token.IsCancellationRequested && !_isZooming)
                        _isManipulating = true;
                });
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        _isPressed = true;
    }

    private void StartPinch()
    {
        var points = _activePointers.Values.ToArray();
        if (points.Length < 2) return;

        _calculator.StartPinch(points[0], points[1]);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_activePointers.ContainsKey(e.Pointer)) return;

        var localPos = e.GetPosition(this);
        var parentPos = LocalToParent(localPos);

        _activePointers[e.Pointer] = parentPos;

        if (_isZooming && _activePointers.Count >= 2)
        {
            UpdatePinch();
            return;
        }

        if (_isZooming) return;

        var currentPoint = e.GetCurrentPoint(this);
        var isMouseDragging = currentPoint.Properties.IsLeftButtonPressed;
        var isTouchOrPen = currentPoint.Pointer.Type is PointerType.Touch or PointerType.Pen
                           && _isPressed;
        if (!isMouseDragging && !isTouchOrPen) return;

        var delta = parentPos - _lastPointerPosition;
        _lastPointerPosition = parentPos;

        if (!_isManipulating && (Math.Abs(delta.X) > DragThresholdPixels || Math.Abs(delta.Y) > DragThresholdPixels))
        {
            _isManipulating = true;
            _manipulationTokenSource?.Cancel();
        }

        if (!_isManipulating) return;

        _calculator.Pan(delta);
        SyncTransform();
    }

    private void UpdatePinch()
    {
        var points = _activePointers.Values.ToArray();
        if (points.Length < 2) return;

        if (_calculator.UpdatePinch(points[0], points[1]))
            SyncTransform();
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var wasPressed = _isPressed;
        var wasManipulating = _isManipulating;
        var wasZooming = _isZooming;

        _activePointers.Remove(e.Pointer);
        try { e.Pointer.Capture(null); } catch { /* ignore */ }

        if (wasZooming)
        {
            if (_activePointers.Count >= 2)
            {
                StartPinch();
            }
            else
            {
                _activePointers.Clear();
                _suppressCapture = true;

                _isZooming = false;
                _isManipulating = false;
                _isPressed = false;
                _calculator.ResetPinchSmoothing();
            }
            return;
        }

        _manipulationTokenSource?.Cancel();
        _isPressed = false;
        _isManipulating = false;

        if (_suppressCapture && e.Pointer.Type is PointerType.Touch or PointerType.Pen)
        {
            _suppressCapture = false;
            return;
        }

        if (wasPressed && !wasManipulating)
        {
            _clickPosition = e.GetPosition(this);
            if (_clickPosition.HasValue)
                ContentClicked?.Invoke(this, _clickPosition.Value);
        }
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _activePointers.Remove(e.Pointer);
        _suppressCapture = false;

        _manipulationTokenSource?.Cancel();
        _isZooming = false;
        _isPressed = false;
        _isManipulating = false;
        _calculator.ResetPinchSmoothing();
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var parentPos = LocalToParent(e.GetPosition(this));
        var delta = e.Delta.Y * ScaleStep;
        if (_calculator.ApplyZoom(1 + delta, parentPos))
            SyncTransform();
    }

    public void CenterMap()
    {
        _calculator.Center();
        SyncTransform();
    }

    public void ResetZoom()
    {
        _calculator.ResetZoom();
        SyncTransform();
    }

    public byte[] ToPng()
    {
        var saved = _mapTransform.Matrix;
        _mapTransform.Matrix = new Matrix(1, 0, 0, 1, 0, 0);
        try
        {
            var w = double.IsNaN(Width) ? (int)Bounds.Width : (int)Width;
            var h = double.IsNaN(Height) ? (int)Bounds.Height : (int)Height;
            return this.RenderToPngBytes(w, h);
        }
        finally
        {
            _mapTransform.Matrix = saved;
        }
    }
}
