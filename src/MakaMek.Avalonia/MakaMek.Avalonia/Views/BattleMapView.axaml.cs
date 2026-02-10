using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Sanet.MakaMek.Avalonia.Controls;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MVVM.Views.Avalonia;

namespace Sanet.MakaMek.Avalonia.Views;

public partial class BattleMapView : BaseView<BattleMapViewModel>
{
    private Point _lastPointerPosition;
    private readonly TranslateTransform _mapTranslateTransform = new();
    private readonly ScaleTransform _mapScaleTransform = new() { ScaleX = 1, ScaleY = 1 };
    private const double MinScale = 0.5;
    private const double MaxScale = 2.0;
    private const double ScaleStep = 0.1;
    private const int SelectionThresholdMilliseconds = 250; // Time to distinguish selection vs. pan
    private bool _isManipulating;
    private bool _isZooming;
    private bool _isPressed;
    private CancellationTokenSource _manipulationTokenSource;
    private List<UnitControl>? _unitControls;
    private readonly List<PathSegmentControl> _movementPathSegments = [];
    private readonly List<WeaponAttackControl> _weaponAttackControls = [];
    private Point? _clickPosition;
    private HexControl? _selectedHex;

    public BattleMapView()
    {
        InitializeComponent();
        
        var transformGroup = new TransformGroup();
        transformGroup.Children.Add(_mapScaleTransform);
        transformGroup.Children.Add(_mapTranslateTransform);
        MapCanvas.RenderTransform = transformGroup;
        
        MapCanvas.PointerPressed += OnPointerPressed;
        MapCanvas.PointerMoved += OnPointerMoved;
        MapCanvas.PointerReleased += OnPointerReleased;
        MapCanvas.PointerWheelChanged += OnPointerWheelChanged;
        
        var pinchGestureRecognizer = new PinchGestureRecognizer();
        MapCanvas.GestureRecognizers.Add(pinchGestureRecognizer);
        MapCanvas.AddHandler(Gestures.PinchEvent, OnPinchChanged);
        MapCanvas.AddHandler(Gestures.PinchEndedEvent, OnPinchEnded);
    }

    private void RenderMap(IGame game, IImageService<Bitmap> imageService)
    {
        var directionSelector = DirectionSelector;
        MapCanvas.Children.Clear();

        var maxH = 0d;
        var maxV = 0d;
        
        foreach (var hex in game.BattleMap?.GetHexes()??[])
        {
            var hexControl = new HexControl(hex, imageService);
            MapCanvas.Children.Add(hexControl);
            if (hex.Coordinates.H > maxH) maxH = hex.Coordinates.H;
            if (hex.Coordinates.V > maxV) maxV = hex.Coordinates.V;
        }
        
        _unitControls = ViewModel?.Units
            .Select(u=>new UnitControl(u, (IImageService<Bitmap>)ViewModel.ImageService, ViewModel))
            .ToList();
        if (_unitControls != null)
        {
            foreach (var unitControl in _unitControls)
            {
                MapCanvas.Children.Add(unitControl);
            }
        }

        // Ensure DirectionSelector stays on top
        MapCanvas.Children.Add(directionSelector);
        
        MapCanvas.Width = maxH + 2*HexCoordinates.HexWidth;
        MapCanvas.Height = maxV + 3*HexCoordinates.HexHeight; //this is a bit of a workaround to fit the menu
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_isZooming) return;
        _lastPointerPosition = e.GetPosition(this);
        
        _isManipulating = false; // Reset manipulation flag

        // Start a timer to determine if this is a manipulation
        _manipulationTokenSource = new CancellationTokenSource();
        Task.Delay(SelectionThresholdMilliseconds, _manipulationTokenSource.Token)
            .ContinueWith(t =>
            {
                if (!t.IsCanceled)
                {
                    _isManipulating = true; // Set the flag if the delay completes
                }
            }, TaskScheduler.Current);
        _isPressed = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs? e)
    {
        // Cancel the manipulation timer
        _manipulationTokenSource.Cancel();

        if (!_isManipulating)
        {
            if (!_isPressed) return;
            _isPressed = false;
            
            _clickPosition = e?.GetPosition(MapCanvas);
            if (!_clickPosition.HasValue) return;

            // Handle DirectionSelector interaction
            if (DirectionSelector.IsVisible)
            {
                if (DirectionSelector.Bounds.Contains(_clickPosition.Value))
                {
                    var directionSelectorPosition = _clickPosition.Value - DirectionSelector.Bounds.Position;
                    if (DirectionSelector.HandleInteraction(directionSelectorPosition)) return;
                }
            }

            // Handle UnitControl interactions
            if (_unitControls != null)
            {
                foreach (var unit in _unitControls)
                {
                    if (!unit.ActionButtons.Bounds.Contains(_clickPosition.Value)) continue;
                    var unitPosition = _clickPosition.Value - unit.ActionButtons.Bounds.Position;
                    if (unit.HandleInteraction(unitPosition)) return;
                }
            }

            // If no controls were interacted with, handle hex selection
            _selectedHex = MapCanvas.Children
                .OfType<HexControl>()
                .FirstOrDefault(h => h.IsPointInside(_clickPosition.Value));

            if (_selectedHex != null && ViewModel!=null)
            {
                // Assign the hex coordinates to the ViewModel's unit position
                ViewModel?.HandleHexSelection(_selectedHex.Hex);
            }
        }
    }
    

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isZooming) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var position = e.GetPosition(this);
        var delta = position - _lastPointerPosition;
        _lastPointerPosition = position;
        
        _mapTranslateTransform.X += delta.X;
        _mapTranslateTransform.Y += delta.Y;
    }
    
    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var delta = e.Delta.Y * ScaleStep;
        ApplyZoom(1 + delta, e.GetPosition(MapCanvas));
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
        MapCanvas.RenderTransformOrigin = new RelativePoint(new Point(0.5, 0.5), RelativeUnit.Relative);
    }
    
    private void ApplyZoom(double scaleFactor, Point origin)
    {
        if (origin.X < 0 || origin.Y < 0) return;
        if (origin.X > MapCanvas.Width || origin.Y > MapCanvas.Height) return;
        
        var newScale = _mapScaleTransform.ScaleX * scaleFactor;
        if (newScale is < MinScale or > MaxScale) return;
    
        MapCanvas.RenderTransformOrigin = new RelativePoint(origin, RelativeUnit.Absolute);
        _mapScaleTransform.ScaleX = newScale;
        _mapScaleTransform.ScaleY = newScale;
    }

    protected override void OnViewModelSet()
    {
        base.OnViewModelSet();
        if (ViewModel is { Game: not null })
        {
            RenderMap(ViewModel.Game, (IImageService<Bitmap>)ViewModel.ImageService);
            ViewModel.PropertyChanged+=OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.MovementPath))
        {
            UpdateMovementPath();
        }
        else if (e.PropertyName == nameof(ViewModel.WeaponAttacks))
        {
            UpdateWeaponAttacks();
        }
    }

    private void UpdateMovementPath()
    {
        if (ViewModel == null) return;
        if (ViewModel.MovementPath == null)
        {
            foreach (var pathSegmentControl in _movementPathSegments)
            {
                MapCanvas.Children.Remove(pathSegmentControl);
            }
            _movementPathSegments.Clear();
            return;
        }

        var color = Color.TryParse(ViewModel.ActivePlayerTint, out var parsed)
            ? parsed
            : Colors.Yellow;

        foreach (var pathSegmentViewModel in ViewModel.MovementPath)
        {
            var segmentControl = new PathSegmentControl(pathSegmentViewModel, color);
            MapCanvas.Children.Add(segmentControl);
            _movementPathSegments.Add(segmentControl);
        }
    }

    private void UpdateWeaponAttacks()
    {
        // Clear existing attacks
        foreach (var control in _weaponAttackControls)
        {
            MapCanvas.Children.Remove(control);
        }
        _weaponAttackControls.Clear();
    
        if (ViewModel?.WeaponAttacks == null) return;
    
        foreach (var attack in ViewModel.WeaponAttacks)
        {
            var control = new WeaponAttackControl(attack);
            _weaponAttackControls.Add(control);
            MapCanvas.Children.Add(control);
        }
    }

    private void CenterMap(object? sender, RoutedEventArgs e)
    {
        _mapTranslateTransform.X = 0;
        _mapTranslateTransform.Y = 0;
        _mapScaleTransform.ScaleX = 1;
        _mapScaleTransform.ScaleY = 1;
        MapCanvas.RenderTransformOrigin = new RelativePoint(new Point(0.5, 0.5), RelativeUnit.Relative);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        TurnInfoPanel.MaxWidth = Math.Max(e.NewSize.Width-6, 300);
    }
}
