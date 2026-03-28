using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Avalonia.Controls;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MakaMek.Services;
using Sanet.MVVM.Views.Avalonia;

namespace Sanet.MakaMek.Avalonia.Views;

public partial class BattleMapView : BaseView<BattleMapViewModel>
{
    private List<UnitControl>? _unitControls;
    private readonly List<PathSegmentControl> _movementPathSegments = [];
    private readonly List<WeaponAttackControl> _weaponAttackControls = [];
    private HexControl? _selectedHex;

    public BattleMapView()
    {
        InitializeComponent();

        MapCanvas.ContentClicked += OnMapContentClicked;
    }

    private void RenderMap(IGame game)
    {
        var terrainAssetService = ViewModel?.TerrainAssetService;
        if (terrainAssetService == null)
        {
            game.Logger.LogError("Terrain asset service is not available");
            return;
        }
        var directionSelector = DirectionSelector;
        MapCanvas.Children.Clear();
        _movementPathSegments.Clear();
        _weaponAttackControls.Clear();

        var maxH = 0d;
        var maxV = 0d;

        var hexConfiguration = ViewModel?.HexConfiguration.ToConfiguration();

        foreach (var hex in game.BattleMap?.GetHexes()??[])
        {
            var edges = game.BattleMap?.GetHexEdges(hex.Coordinates) ?? [];
            var hexControl = new HexControl(hex, game.Logger, terrainAssetService, edges, hexConfiguration);
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

        MapCanvas.Width = maxH + 2*HexCoordinatesPixelExtensions.HexWidth;
        MapCanvas.Height = maxV + 3*HexCoordinatesPixelExtensions.HexHeight; //this is a bit of a workaround to fit the menu
        
        // restore overlays after a full canvas rebuild
        UpdateMovementPath();
        UpdateWeaponAttacks();
    }

    private void OnMapContentClicked(object? sender, Point clickPosition)
    {
        // Handle DirectionSelector interaction
        if (DirectionSelector.IsVisible)
        {
            if (DirectionSelector.Bounds.Contains(clickPosition))
            {
                var directionSelectorPosition = clickPosition - DirectionSelector.Bounds.Position;
                if (DirectionSelector.HandleInteraction(directionSelectorPosition)) return;
            }
        }

        // Handle UnitControl interactions
        if (_unitControls != null)
        {
            foreach (var unit in _unitControls)
            {
                if (!unit.ActionButtons.Bounds.Contains(clickPosition)) continue;
                var unitPosition = clickPosition - unit.ActionButtons.Bounds.Position;
                if (unit.HandleInteraction(unitPosition)) return;
            }
        }

        // If no controls were interacted with, handle hex selection
        _selectedHex = MapCanvas.Children
            .OfType<HexControl>()
            .FirstOrDefault(h => h.IsPointInside(clickPosition));

        if (_selectedHex != null && ViewModel!=null)
        {
            // Assign the hex coordinates to the ViewModel's unit position
            ViewModel?.HandleHexSelection(_selectedHex.Hex);
        }
    }

    protected override void OnViewModelSet()
    {
        base.OnViewModelSet();
        if (ViewModel is { Game: not null })
        {
            RenderMap(ViewModel.Game);
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
        else if (e.PropertyName == nameof(ViewModel.HexConfiguration))
        {
            if (ViewModel?.Game != null)
            {
                RenderMap(ViewModel.Game);
            }
        }
        else if (e.PropertyName == nameof(ViewModel.AvailableActions))
        {
            UpdateMobileActionButtons();
        }
    }

    private void UpdateMobileActionButtons()
    {
        if (ViewModel is not { IsMobile: true }) return;

        MobileActionButtonsPanel.Children.Clear();

        foreach (var action in ViewModel.AvailableActions)
        {
            if (!action.IsVisible) continue;

            var button = new Button
            {
                Content = action.Label,
                Padding = new Thickness(20, 12),
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 16,
                FontWeight = FontWeight.Bold,
                Background = new SolidColorBrush(Colors.Aqua)
            };

            var capturedAction = action;
            button.Click += (_, _) => capturedAction.OnExecute();
            MobileActionButtonsPanel.Children.Add(button);
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
        MapCanvas.CenterMap();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        TurnInfoPanel.MaxWidth = Math.Max(e.NewSize.Width-6, 300);
    }
}
