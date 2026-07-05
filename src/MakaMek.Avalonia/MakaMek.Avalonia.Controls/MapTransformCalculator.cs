using Avalonia;

namespace Sanet.MakaMek.Avalonia.Controls;

/// <summary>
/// Encapsulates the pure pan/zoom/pinch transform math for a pannable, zoomable
/// canvas. This type has no dependency on any control or on Avalonia's event
/// system, so it can be reused by other controls and unit-tested in isolation.
///
/// The state is a single affine matrix of the form:
///   | s 0 tx |
///   | 0 s ty |
///   | 0 0 1  |
/// Applied to local point L: parent = (L.X*s + tx, L.Y*s + ty)
/// </summary>
public class MapTransformCalculator
{
    private const double DistanceSmoothingAlpha = 0.35;

    // Pinch baseline. The anchor is captured in LOCAL coords at gesture start
    // and stays fixed for the gesture's lifetime — the local content point
    // under the start midpoint should always end up under the current midpoint.
    private double _pinchStartDistance;
    private double _pinchStartScale;
    private Point _pinchAnchorLocal;

    private double _smoothedDistanceRatio = 1.0;
    private bool _pinchSmoothingInitialized;

    public double Scale { get; private set; } = 1.0;
    public double TranslateX { get; private set; }
    public double TranslateY { get; private set; }

    private double _minScale = 0.5;
    private double _maxScale = 2.0;

    /// <summary>
    /// Lower bound for zoom. Clamped to [0, MaxScale] on assignment so that
    /// MinScale &lt;= MaxScale is always satisfied.
    /// </summary>
    public double MinScale
    {
        get => _minScale;
        set => _minScale = Math.Clamp(value, 0, _maxScale);
    }

    /// <summary>
    /// Upper bound for zoom. Clamped to [MinScale, ∞) on assignment so that
    /// MaxScale &gt;= MinScale is always satisfied.
    /// </summary>
    public double MaxScale
    {
        get => _maxScale;
        set => _maxScale = Math.Max(value, _minScale);
    }

    /// <summary>
    /// The current transform expressed as an Avalonia <see cref="Matrix"/>.
    /// </summary>
    public Matrix Matrix => new(Scale, 0, 0, Scale, TranslateX, TranslateY);

    public void SetTransform(double scale, double translateX, double translateY)
    {
        Scale = scale;
        TranslateX = translateX;
        TranslateY = translateY;
    }

    /// <summary>
    /// Convert local coords (what a control returns for its own space) to parent
    /// coords using the CURRENT matrix: parent = local * s + t.
    /// </summary>
    public Point LocalToParent(Point local) =>
        new(local.X * Scale + TranslateX, local.Y * Scale + TranslateY);

    /// <summary>
    /// Pan by the given delta expressed in parent coords.
    /// </summary>
    public void Pan(Point delta) =>
        SetTransform(Scale, TranslateX + delta.X, TranslateY + delta.Y);

    /// <summary>
    /// Zoom by <paramref name="scaleFactor"/> anchored at <paramref name="originParent"/>
    /// (in parent coords). The resulting scale is clamped to [MinScale, MaxScale].
    /// Returns <c>true</c> if the transform actually changed.
    /// </summary>
    public bool ApplyZoom(double scaleFactor, Point originParent)
    {
        var currentScale = Scale;
        var newScale = Math.Clamp(currentScale * scaleFactor, MinScale, MaxScale);
        var actualFactor = newScale / currentScale;
        if (Math.Abs(actualFactor - 1.0) < 1e-9) return false;

        // Anchor at originParent: newTranslate = origin - (origin - t) * factor
        var newTx = originParent.X - (originParent.X - TranslateX) * actualFactor;
        var newTy = originParent.Y - (originParent.Y - TranslateY) * actualFactor;
        SetTransform(newScale, newTx, newTy);
        return true;
    }

    /// <summary>
    /// Begin (or re-baseline) a pinch gesture from two pointer positions in parent coords.
    /// </summary>
    public void StartPinch(Point p0, Point p1)
    {
        _pinchStartDistance = Distance(p0, p1);
        var startMidParent = Midpoint(p0, p1);

        _pinchStartScale = Scale;
        var startTx = TranslateX;
        var startTy = TranslateY;

        // Capture the LOCAL content point under the start midpoint.
        // local = (parent - translate) / scale
        _pinchAnchorLocal = new Point(
            (startMidParent.X - startTx) / _pinchStartScale,
            (startMidParent.Y - startTy) / _pinchStartScale);

        _smoothedDistanceRatio = 1.0;
        _pinchSmoothingInitialized = false;
    }

    /// <summary>
    /// Update an in-progress pinch gesture from two pointer positions in parent coords.
    /// Keeps the anchor local point under the moving finger midpoint while scaling.
    /// Returns <c>true</c> if the transform was updated.
    /// </summary>
    public bool UpdatePinch(Point p0, Point p1)
    {
        if (_pinchStartDistance <= 0) return false;

        var currentDistance = Distance(p0, p1);
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

        var targetScale = Math.Clamp(_pinchStartScale * _smoothedDistanceRatio, MinScale, MaxScale);
        var currentMidParent = Midpoint(p0, p1);

        // We want the anchor local point to render at currentMidParent:
        //   currentMidParent = anchorLocal * targetScale + newTranslate
        var newTx = currentMidParent.X - _pinchAnchorLocal.X * targetScale;
        var newTy = currentMidParent.Y - _pinchAnchorLocal.Y * targetScale;

        SetTransform(targetScale, newTx, newTy);
        return true;
    }

    /// <summary>
    /// Reset the pinch smoothing state (call when a pinch gesture ends).
    /// </summary>
    public void ResetPinchSmoothing()
    {
        _pinchSmoothingInitialized = false;
        _smoothedDistanceRatio = 1.0;
    }

    /// <summary>
    /// Reset scale and translation to identity (centred, unzoomed).
    /// </summary>
    public void Center() => SetTransform(1, 0, 0);

    /// <summary>
    /// Reset scale to 1 while keeping the current translation.
    /// </summary>
    public void ResetZoom() => SetTransform(1, TranslateX, TranslateY);

    public static double Distance(Point a, Point b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    public static Point Midpoint(Point a, Point b) =>
        new((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);
}
