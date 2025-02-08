using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Sanet.MekForge.Avalonia.Controls;
using Sanet.MekForge.Core.Models.Game;
using Sanet.MekForge.Core.Services;
using Sanet.MekForge.Core.ViewModels;
using Sanet.MVVM.Views.Avalonia;

namespace Sanet.MekForge.Avalonia.Views;

public partial class BattleMapView : BaseView<BattleMapViewModel>
{
    private Point _lastPointerPosition;
    private readonly TranslateTransform _mapTranslateTransform = new();
    private readonly ScaleTransform _mapScaleTransform = new() { ScaleX = 1, ScaleY = 1 };
    private const double MinScale = 0.5;
    private const double MaxScale = 2.0;
    private const double ScaleStep = 0.1;
    private const int SelectionThresholdMilliseconds = 250; // Time to distinguish selection vs pan
    private bool _isManipulating;
    private bool _isPressed;
    private CancellationTokenSource _manipulationTokenSource;
    private List<UnitControl>? _unitControls;
    private List<PathSegmentControl> _movementPathSegments = [];
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
    }

    private void RenderMap(IGame game, IImageService<Bitmap> imageService)
    {
        var directionSelector = DirectionSelector;
        MapCanvas.Children.Clear();

        foreach (var hex in game.BattleMap.GetHexes())
        {
            var hexControl = new HexControl(hex, imageService);
            MapCanvas.Children.Add(hexControl);
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
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _lastPointerPosition = e.GetPosition(this);
        
        _isManipulating = false; // Reset manipulation flag

        // Start a timer to determine if this is a manipulation
        _manipulationTokenSource = new CancellationTokenSource();
        Task.Delay(SelectionThresholdMilliseconds, _manipulationTokenSource.Token)
            .ContinueWith(t =>
            {
                if (!t.IsCanceled)
                {
                    _isManipulating = true; // Set flag if the delay completes
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
        var newScale = _mapScaleTransform.ScaleX + delta;

        if (!(newScale >= MinScale) || !(newScale <= MaxScale)) return;
        _mapScaleTransform.ScaleX = newScale;
        _mapScaleTransform.ScaleY = newScale;
    }
    
    private void OnPinchChanged(object? sender, PinchEventArgs e)
    {
        var newScale = _mapScaleTransform.ScaleX * e.Scale;

        if (!(newScale >= MinScale) || !(newScale <= MaxScale)) return;
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
        if (e.PropertyName != nameof(ViewModel.MovementPath)) return;
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

        foreach (var pathSegmentViewModel in ViewModel.MovementPath)
        {
            var segmentControl = new PathSegmentControl(pathSegmentViewModel, ViewModel);
            MapCanvas.Children.Add(segmentControl);
            _movementPathSegments.Add(segmentControl);
        }
    }
}
