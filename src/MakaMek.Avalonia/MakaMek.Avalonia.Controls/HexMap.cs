using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Sanet.MakaMek.Avalonia.Controls;

/// <summary>
/// A canvas control with built-in pan and zoom functionality for hex-based maps.
/// </summary>
public class HexMap : Canvas
{
    private Point _lastPointerPosition;
    private readonly TranslateTransform _mapTranslateTransform = new();
    private readonly ScaleTransform _mapScaleTransform = new() { ScaleX = 1, ScaleY = 1 };
    private const int SelectionThresholdMilliseconds = 250; // Time to distinguish selection vs. pan
    private const double DragThresholdPixels = 3.0; 
    private bool _isManipulating;
    private bool _isZooming;
    private bool _isPressed;
    private CancellationTokenSource? _manipulationTokenSource;
    private Point? _clickPosition;

    /// <summary>
    /// Gets or sets the minimum scale factor for zooming.
    /// </summary>
    public double MinScale { get; set; } = 0.5;

    /// <summary>
    /// Gets or sets the maximum scale factor for zooming.
    /// </summary>
    public double MaxScale { get; set; } = 2.0;

    /// <summary>
    /// Gets or sets the scale step for zoom operations.
    /// </summary>
    public double ScaleStep { get; set; } = 0.1;

    /// <summary>
    /// Event raised when the content is clicked (not dragged).
    /// Provides the click position in canvas coordinates.
    /// </summary>
    public event EventHandler<Point>? ContentClicked;

    public HexMap()
    {
        var transformGroup = new TransformGroup();
        transformGroup.Children.Add(_mapScaleTransform);
        transformGroup.Children.Add(_mapTranslateTransform);
        RenderTransform = transformGroup;

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerWheelChanged += OnPointerWheelChanged;

        var pinchGestureRecognizer = new PinchGestureRecognizer();
        GestureRecognizers.Add(pinchGestureRecognizer);
        AddHandler(Gestures.PinchEvent, OnPinchChanged);
        AddHandler(Gestures.PinchEndedEvent, OnPinchEnded);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_isZooming) return;
        _lastPointerPosition = e.GetPosition(this.Parent as Visual ?? this);

        _isManipulating = false; // Reset manipulation flag

        // Start a timer to determine if this is a manipulation
        _manipulationTokenSource?.Cancel();
        _manipulationTokenSource?.Dispose();
        _manipulationTokenSource = new CancellationTokenSource();
        Task.Delay(SelectionThresholdMilliseconds, _manipulationTokenSource.Token)
            .ContinueWith(t =>
            {
                if (!t.IsCanceled)
                {
                    _isManipulating = true; // Set the flag if the delay completes
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        _isPressed = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs? e)
    {
        // Cancel the manipulation timer
        _manipulationTokenSource?.Cancel();

        if (!_isManipulating)
        {
            if (!_isPressed) return;
            _isPressed = false;

            _clickPosition = e?.GetPosition(this);
            if (_clickPosition.HasValue)
            {
                // Raise the ContentClicked event for consumers to handle
                ContentClicked?.Invoke(this, _clickPosition.Value);
            }
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isZooming) return;
        if (!e.GetCurrentPoint(this.Parent as Visual ?? this).Properties.IsLeftButtonPressed) return;
        var position = e.GetPosition(this.Parent as Visual ?? this);
        var delta = position - _lastPointerPosition;
        _lastPointerPosition = position;

        if (!_isManipulating && (Math.Abs(delta.X) > DragThresholdPixels || Math.Abs(delta.Y) > DragThresholdPixels))
        {
            _isManipulating = true;
            _manipulationTokenSource?.Cancel();
        }

        _mapTranslateTransform.X += delta.X;
        _mapTranslateTransform.Y += delta.Y;
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var delta = e.Delta.Y * ScaleStep;
        ApplyZoom(1 + delta, e.GetPosition(this));
    }

    private void OnPinchChanged(object? sender, PinchEventArgs e)
    {
        _isManipulating = true;
        _isZooming = true;
        ApplyZoom(e.Scale, e.ScaleOrigin);
    }

    private void OnPinchEnded(object? sender, PinchEndedEventArgs e)
    {
        _isManipulating = false;
        _isZooming = false;
        RenderTransformOrigin = new RelativePoint(new Point(0.5, 0.5), RelativeUnit.Relative);
    }

    private void ApplyZoom(double scaleFactor, Point origin)
    {
        if (origin.X < 0 || origin.Y < 0) return;
        if (origin.X > Bounds.Width || origin.Y > Bounds.Height) return;

        var newScale = _mapScaleTransform.ScaleX * scaleFactor;
        if (newScale < MinScale || newScale > MaxScale) return;

        RenderTransformOrigin = new RelativePoint(origin, RelativeUnit.Absolute);
        _mapScaleTransform.ScaleX = newScale;
        _mapScaleTransform.ScaleY = newScale;
    }

    /// <summary>
    /// Resets the map to its default position and zoom level.
    /// </summary>
    public void CenterMap()
    {
        _mapTranslateTransform.X = 0;
        _mapTranslateTransform.Y = 0;
        _mapScaleTransform.ScaleX = 1;
        _mapScaleTransform.ScaleY = 1;
        RenderTransformOrigin = new RelativePoint(new Point(0.5, 0.5), RelativeUnit.Relative);
    }

    /// <summary>
    /// Resets only the zoom level to 1.0, maintaining the current pan position.
    /// </summary>
    public void ResetZoom()
    {
        _mapScaleTransform.ScaleX = 1;
        _mapScaleTransform.ScaleY = 1;
        RenderTransformOrigin = new RelativePoint(new Point(0.5, 0.5), RelativeUnit.Relative);
    }
}
