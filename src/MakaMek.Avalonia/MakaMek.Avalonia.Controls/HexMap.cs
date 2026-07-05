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

    // Single matrix transform. Using one MatrixTransform instead of
    // TransformGroup([Scale, Translate]) removes any ambiguity about
    // child-order conventions. The matrix is:
    //   | s 0 tx |
    //   | 0 s ty |
    //   | 0 0 1  |
    // Applied to local point L: parent = (L.X*s + tx, L.Y*s + ty)
    private readonly MatrixTransform _mapTransform = new()
    {
        Matrix = new Matrix(1, 0, 0, 1, 0, 0)
    };

    private const int SelectionThresholdMilliseconds = 250;
    private const double DragThresholdPixels = 3.0;
    private bool _isManipulating;
    private bool _isZooming;
    private bool _isPressed;
    private CancellationTokenSource? _manipulationTokenSource;
    private Point? _clickPosition;

    private readonly Dictionary<IPointer, Point> _activePointers = new();

    // Pinch baseline. The anchor is captured in LOCAL coords at gesture start
    // and stays fixed for the gesture's lifetime — the local content point
    // under the start midpoint should always end up under the current midpoint.
    private double _pinchStartDistance;
    private double _pinchStartMapScale;
    private Point _pinchAnchorLocal;

    private double _smoothedDistanceRatio = 1.0;
    private bool _pinchSmoothingInitialized;
    private const double DistanceSmoothingAlpha = 0.35;

    public double MinScale { get; set; } = 0.5;
    public double MaxScale { get; set; } = 2.0;
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

    private double CurrentScale => _mapTransform.Matrix.M11;
    private double CurrentTranslateX => _mapTransform.Matrix.M31;
    private double CurrentTranslateY => _mapTransform.Matrix.M32;

    /// <summary>
    /// Convert HexMap-local coords (what e.GetPosition(this) returns) to
    /// parent coords using the CURRENT matrix: parent = local * s + t.
    /// This is unambiguous — no reliance on e.GetPosition(parent) returning
    /// the right space, and no reliance on TransformGroup ordering.
    /// </summary>
    private Point LocalToParent(Point local)
    {
        var s = CurrentScale;
        return new Point(
            local.X * s + CurrentTranslateX,
            local.Y * s + CurrentTranslateY);
    }

    private void SetTransform(double scale, double translateX, double translateY)
    {
        _mapTransform.Matrix = new Matrix(scale, 0, 0, scale, translateX, translateY);
    }

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

        _pinchStartDistance = Distance(points[0], points[1]);
        var startMidParent = Midpoint(points[0], points[1]);

        _pinchStartMapScale = CurrentScale;
        var startTx = CurrentTranslateX;
        var startTy = CurrentTranslateY;

        // Capture the LOCAL content point under the start midpoint.
        // local = (parent - translate) / scale
        // This anchor stays fixed for the gesture — it's the content point
        // that should remain under the (moving) finger midpoint.
        _pinchAnchorLocal = new Point(
            (startMidParent.X - startTx) / _pinchStartMapScale,
            (startMidParent.Y - startTy) / _pinchStartMapScale);

        _smoothedDistanceRatio = 1.0;
        _pinchSmoothingInitialized = false;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        // Always compute parent position via explicit local→parent conversion
        var localPos = e.GetPosition(this);
        var parentPos = LocalToParent(localPos);

        if (_activePointers.ContainsKey(e.Pointer))
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
        SetTransform(CurrentScale, CurrentTranslateX + delta.X, CurrentTranslateY + delta.Y);
    }

    private void UpdatePinch()
    {
        var points = _activePointers.Values.ToArray();
        if (points.Length < 2 || _pinchStartDistance <= 0) return;

        var currentDistance = Distance(points[0], points[1]);
        var rawRatio = currentDistance / _pinchStartDistance;

        if (!_pinchSmoothingInitialized)
        {
            _smoothedDistanceRatio = rawRatio;
            _pinchSmoothingInitialized = true;
        }
        else
        {
            _smoothedDistanceRatio += (rawRatio - _smoothedDistanceRatio) * DistanceSmoothingAlpha;
        }

        var targetScale = Math.Clamp(_pinchStartMapScale * _smoothedDistanceRatio, MinScale, MaxScale);
        var currentMidParent = Midpoint(points[0], points[1]);

        // We want the anchor local point to render at currentMidParent:
        //   currentMidParent = anchorLocal * targetScale + newTranslate
        //   newTranslate = currentMidParent - anchorLocal * targetScale
        var newTx = currentMidParent.X - _pinchAnchorLocal.X * targetScale;
        var newTy = currentMidParent.Y - _pinchAnchorLocal.Y * targetScale;

        SetTransform(targetScale, newTx, newTy);
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
                _pinchSmoothingInitialized = false;
                _smoothedDistanceRatio = 1.0;

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
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var parentPos = LocalToParent(e.GetPosition(this));
        var delta = e.Delta.Y * ScaleStep;
        ApplyZoom(1 + delta, parentPos);
    }

    private void ApplyZoom(double scaleFactor, Point originParent)
    {
        var currentScale = CurrentScale;
        var newScale = Math.Clamp(currentScale * scaleFactor, MinScale, MaxScale);
        var actualFactor = newScale / currentScale;
        if (Math.Abs(actualFactor - 1.0) < 1e-9) return;

        var currentTx = CurrentTranslateX;
        var currentTy = CurrentTranslateY;
        // Anchor at originParent: newTranslate = origin - (origin - t) * factor
        var newTx = originParent.X - (originParent.X - currentTx) * actualFactor;
        var newTy = originParent.Y - (originParent.Y - currentTy) * actualFactor;
        SetTransform(newScale, newTx, newTy);
    }

    private static double Distance(Point a, Point b)
        => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static Point Midpoint(Point a, Point b)
        => new((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);

    public void CenterMap() => SetTransform(1, 0, 0);

    public void ResetZoom() => SetTransform(1, CurrentTranslateX, CurrentTranslateY);

    public byte[] ToPng()
    {
        var saved = _mapTransform.Matrix;
        SetTransform(1, 0, 0);
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