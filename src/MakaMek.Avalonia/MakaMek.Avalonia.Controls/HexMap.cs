using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Sanet.MakaMek.Avalonia.Controls.Extensions;

namespace Sanet.MakaMek.Avalonia.Controls;

/// <summary>
/// A canvas control with built-in pan and zoom functionality for hex-based maps.
/// </summary>
public class HexMap : Canvas
{
    private Point _lastPointerPosition;

    // Single matrix transform bound to RenderTransform. The transform math lives
    // in _calculator; after any state change we sync the matrix from it.
    private readonly MatrixTransform _mapTransform = new()
    {
        Matrix = new Matrix(1, 0, 0, 1, 0, 0)
    };

    // Reusable, Avalonia-event-free pan/zoom/pinch math.
    private readonly MapTransformCalculator _calculator = new();

    private const int SelectionThresholdMilliseconds = 250;
    private const double DragThresholdPixels = 3.0;
    private bool _isManipulating;
    private bool _isZooming;
    private bool _isPressed;
    private CancellationTokenSource? _manipulationTokenSource;
    private Point? _clickPosition;

    private readonly Dictionary<IPointer, Point> _activePointers = new();

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

    /// <summary>
    /// Convert HexMap-local coords (what e.GetPosition(this) returns) to
    /// parent coords using the CURRENT matrix: parent = local * s + t.
    /// </summary>
    private Point LocalToParent(Point local) => _calculator.LocalToParent(local);

    /// <summary>
    /// Push the calculator's current matrix onto the bound RenderTransform.
    /// </summary>
    private void SyncTransform() => _mapTransform.Matrix = _calculator.Matrix;

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Pointer.Type is PointerType.Touch or PointerType.Pen)
        {
            try { e.Pointer.Capture(this); } catch { /* ignore */ }
        }

        // Get position in PARENT coords — explicitly via local, then convert.
        // This is the key fix: e.GetPosition(this) ALWAYS returns local coords,
        // and LocalToParent converts using the current matrix.
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
        // Ignore moves from pointers we are not tracking — using them would
        // reuse stale pan state (_lastPointerPosition) from another pointer.
        if (!_activePointers.ContainsKey(e.Pointer)) return;

        // Always compute parent position via explicit local→parent conversion
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

        // Pan: delta is in parent coords, translate is in parent coords (via matrix)
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
        _activePointers.Remove(e.Pointer);
        try { e.Pointer.Capture(null); } catch { /* ignore */ }

        if (_isZooming)
        {
            if (_activePointers.Count >= 2)
            {
                StartPinch(); // re-baseline with the remaining pair
            }
            else
            {
                _isZooming = false;
                _calculator.ResetPinchSmoothing();

                if (_activePointers.Count == 1)
                {
                    _lastPointerPosition = _activePointers.Values.First();
                    _isPressed = true;
                    _isManipulating = true;
                }
                else
                {
                    _isPressed = false;
                    _isManipulating = false;
                }
            }
            return;
        }

        _manipulationTokenSource?.Cancel();
        var wasPressed = _isPressed;
        var wasManipulating = _isManipulating;
        _isPressed = false;
        _isManipulating = false;

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

        // Fully reset gesture state so later pointer moves can resume normal
        // pan/click handling instead of being stuck in a stale gesture.
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