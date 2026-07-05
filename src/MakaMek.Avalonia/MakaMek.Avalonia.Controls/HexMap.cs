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
    private readonly TranslateTransform _mapTranslateTransform = new();
    private readonly ScaleTransform _mapScaleTransform = new() { ScaleX = 1, ScaleY = 1 };
    private const int SelectionThresholdMilliseconds = 250;
    private const double DragThresholdPixels = 3.0;
    private bool _isManipulating;
    private bool _isZooming;
    private bool _isPressed;
    private CancellationTokenSource? _manipulationTokenSource;
    private Point? _clickPosition;

    // Manual multi-touch tracking. We capture pointers ourselves (instead of
    // using PinchGestureRecognizer) so events keep flowing when fingers drift
    // outside HexMap bounds. PinchGestureRecognizer cannot coexist with
    // explicit Pointer.Capture — that's what broke pinch in the previous attempt.
    private readonly Dictionary<IPointer, Point> _activePointers = new();

    // Pinch baseline captured at the moment the 2nd finger goes down.
    // All zoom updates are computed ABSOLUTELY from this baseline, never
    // incrementally — that prevents jitter from compounding across frames.
    private double _pinchStartDistance;
    private double _pinchStartMapScale;
    private Point _pinchStartMapTranslate;
    private Point _pinchStartMidpointParent;

    // Low-pass-filtered current state to suppress touch jitter (worst near
    // screen edges / camera notches, especially at the top of the display).
    private Point _smoothedMidpointParent;
    private double _smoothedDistanceRatio = 1.0;
    private bool _pinchSmoothingInitialized;
    private const double MidpointSmoothingAlpha = 0.4;
    private const double DistanceSmoothingAlpha = 0.35;

    public double MinScale { get; set; } = 0.5;
    public double MaxScale { get; set; } = 2.0;
    public double ScaleStep { get; set; } = 0.1;

    public event EventHandler<Point>? ContentClicked;

    public HexMap()
    {
        var transformGroup = new TransformGroup();
        transformGroup.Children.Add(_mapScaleTransform);
        transformGroup.Children.Add(_mapTranslateTransform);
        RenderTransform = transformGroup;
        RenderTransformOrigin = new RelativePoint(new Point(0, 0), RelativeUnit.Absolute);

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerWheelChanged += OnPointerWheelChanged;
        PointerCaptureLost += OnPointerCaptureLost;
    }

    private Visual ParentVisual => this.Parent as Visual ?? this;

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Capture touch/pen pointers so PointerMoved keeps firing even when
        // the finger drifts outside HexMap's hit-test area. This is what
        // stops the "freeze then jump" near edges. Mouse is left uncaptured
        // so child controls still get normal hit-testing.
        if (e.Pointer.Type is PointerType.Touch or PointerType.Pen)
        {
            try { e.Pointer.Capture(this); } catch { /* ignore */ }
        }

        var position = e.GetPosition(ParentVisual);
        _activePointers[e.Pointer] = position;

        if (_activePointers.Count >= 2 && !_isZooming)
        {
            // Second finger down → start pinch
            StartPinch();
            _isZooming = true;
            _isManipulating = true;
            _manipulationTokenSource?.Cancel();
            return;
        }

        if (_activePointers.Count >= 2) return; // 3rd+ finger during pinch: just track

        // Single-finger path — set up pan / click detection
        _lastPointerPosition = position;
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
        _pinchStartMidpointParent = Midpoint(points[0], points[1]);
        _pinchStartMapScale = _mapScaleTransform.ScaleX;
        _pinchStartMapTranslate = new Point(_mapTranslateTransform.X, _mapTranslateTransform.Y);
        _smoothedMidpointParent = _pinchStartMidpointParent;
        _smoothedDistanceRatio = 1.0;
        _pinchSmoothingInitialized = false;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var position = e.GetPosition(ParentVisual);

        if (_activePointers.ContainsKey(e.Pointer))
            _activePointers[e.Pointer] = position;

        if (_isZooming && _activePointers.Count >= 2)
        {
            UpdatePinch();
            return;
        }

        if (_isZooming) return;

        var currentPoint = e.GetCurrentPoint(ParentVisual);
        var isMouseDragging = currentPoint.Properties.IsLeftButtonPressed;
        var isTouchOrPen = currentPoint.Pointer.Type is PointerType.Touch or PointerType.Pen
                           && _isPressed;
        if (!isMouseDragging && !isTouchOrPen) return;

        var delta = position - _lastPointerPosition;
        _lastPointerPosition = position;

        if (!_isManipulating && (Math.Abs(delta.X) > DragThresholdPixels || Math.Abs(delta.Y) > DragThresholdPixels))
        {
            _isManipulating = true;
            _manipulationTokenSource?.Cancel();
        }

        if (!_isManipulating) return;
        _mapTranslateTransform.X += delta.X;
        _mapTranslateTransform.Y += delta.Y;
    }

    private void UpdatePinch()
    {
        var points = _activePointers.Values.ToArray();
        if (points.Length < 2 || _pinchStartDistance <= 0) return;

        var currentDistance = Distance(points[0], points[1]);
        var currentMidpoint = Midpoint(points[0], points[1]);
        var rawRatio = currentDistance / _pinchStartDistance;

        if (!_pinchSmoothingInitialized)
        {
            _smoothedDistanceRatio = rawRatio;
            _smoothedMidpointParent = currentMidpoint;
            _pinchSmoothingInitialized = true;
        }
        else
        {
            _smoothedDistanceRatio += (rawRatio - _smoothedDistanceRatio) * DistanceSmoothingAlpha;
            _smoothedMidpointParent = new Point(
                _smoothedMidpointParent.X + (currentMidpoint.X - _smoothedMidpointParent.X) * MidpointSmoothingAlpha,
                _smoothedMidpointParent.Y + (currentMidpoint.Y - _smoothedMidpointParent.Y) * MidpointSmoothingAlpha);
        }

        var targetScale = Math.Clamp(_pinchStartMapScale * _smoothedDistanceRatio, MinScale, MaxScale);
        ApplyZoomAbsolute(targetScale, _smoothedMidpointParent, _pinchStartMapScale, _pinchStartMapTranslate);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _activePointers.Remove(e.Pointer);
        try { e.Pointer.Capture(null); } catch { /* ignore */ }

        if (_isZooming)
        {
            if (_activePointers.Count >= 2)
            {
                // A finger lifted but two remain — restart baseline with the
                // new pair so the zoom ratio doesn't suddenly jump.
                StartPinch();
            }
            else
            {
                // Pinch ended
                _isZooming = false;
                _pinchSmoothingInitialized = false;
                _smoothedDistanceRatio = 1.0;

                if (_activePointers.Count == 1)
                {
                    // One finger remains → seamlessly hand off to pan
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

        // Single-finger release (pan or click)
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
        if (e.Pointer is not null)
            _activePointers.Remove(e.Pointer);
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        // Wheel position arrives in HexMap-local coords; convert to parent
        // coords so ApplyZoom gets the origin in the same space as Translate.
        var localPos = e.GetPosition(this);
        var parentPos = TranslateLocalToParent(localPos);
        var delta = e.Delta.Y * ScaleStep;
        ApplyZoom(1 + delta, parentPos);
    }

    /// <summary>
    /// parentPos = Scale * localPos + Translate (the RenderTransform formula).
    /// </summary>
    private Point TranslateLocalToParent(Point local)
    {
        var s = _mapScaleTransform.ScaleX;
        return new Point(
            local.X * s + _mapTranslateTransform.X,
            local.Y * s + _mapTranslateTransform.Y);
    }

    /// <summary>
    /// Sets the absolute target scale, anchoring <paramref name="originParent"/>
    /// (in PARENT coords) so the point under the finger stays fixed in screen space.
    /// Computed from gesture-start state so jitter in origin doesn't compound.
    /// </summary>
    private void ApplyZoomAbsolute(double targetScale, Point originParent, double startScale, Point startTranslate)
    {
        targetScale = Math.Clamp(targetScale, MinScale, MaxScale);
        var factorFromStart = targetScale / startScale;

        _mapScaleTransform.ScaleX = targetScale;
        _mapScaleTransform.ScaleY = targetScale;

        // Keep origin fixed in parent space:
        //   contentUnderFinger_at_start_local = (originParent - startTranslate) / startScale
        //   originParent = contentUnderFinger_local * targetScale + newTranslate
        //   => newTranslate = originParent - (originParent - startTranslate) * factorFromStart
        _mapTranslateTransform.X = originParent.X - (originParent.X - startTranslate.X) * factorFromStart;
        _mapTranslateTransform.Y = originParent.Y - (originParent.Y - startTranslate.Y) * factorFromStart;
    }

    private void ApplyZoom(double scaleFactor, Point originParent)
    {
        var currentScale = _mapScaleTransform.ScaleX;
        var newScale = Math.Clamp(currentScale * scaleFactor, MinScale, MaxScale);
        var actualFactor = newScale / currentScale;
        if (Math.Abs(actualFactor - 1.0) < 1e-9) return;

        var currentTranslate = new Point(_mapTranslateTransform.X, _mapTranslateTransform.Y);
        _mapScaleTransform.ScaleX = newScale;
        _mapScaleTransform.ScaleY = newScale;
        _mapTranslateTransform.X = originParent.X - (originParent.X - currentTranslate.X) * actualFactor;
        _mapTranslateTransform.Y = originParent.Y - (originParent.Y - currentTranslate.Y) * actualFactor;
    }

    private static double Distance(Point a, Point b)
        => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static Point Midpoint(Point a, Point b)
        => new((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);

    public void CenterMap()
    {
        _mapTranslateTransform.X = 0;
        _mapTranslateTransform.Y = 0;
        _mapScaleTransform.ScaleX = 1;
        _mapScaleTransform.ScaleY = 1;
    }

    public void ResetZoom()
    {
        _mapScaleTransform.ScaleX = 1;
        _mapScaleTransform.ScaleY = 1;
    }

    public byte[] ToPng()
    {
        var savedX = _mapTranslateTransform.X;
        var savedY = _mapTranslateTransform.Y;
        var savedScaleX = _mapScaleTransform.ScaleX;
        var savedScaleY = _mapScaleTransform.ScaleY;

        _mapTranslateTransform.X = 0;
        _mapTranslateTransform.Y = 0;
        _mapScaleTransform.ScaleX = 1;
        _mapScaleTransform.ScaleY = 1;

        try
        {
            var w = double.IsNaN(Width) ? (int)Bounds.Width : (int)Width;
            var h = double.IsNaN(Height) ? (int)Bounds.Height : (int)Height;
            return this.RenderToPngBytes(w, h);
        }
        finally
        {
            _mapTranslateTransform.X = savedX;
            _mapTranslateTransform.Y = savedY;
            _mapScaleTransform.ScaleX = savedScaleX;
            _mapScaleTransform.ScaleY = savedScaleY;
        }
    }
}