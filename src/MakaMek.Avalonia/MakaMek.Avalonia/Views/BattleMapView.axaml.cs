using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Avalonia.Controls;
using Sanet.MakaMek.Avalonia.Services;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MakaMek.Services;
using Sanet.MVVM.Views.Avalonia;

namespace Sanet.MakaMek.Avalonia.Views;

public partial class BattleMapView : BaseView<BattleMapViewModel>
{
    private List<UnitControl>? _unitControls;
    private readonly List<PathSegmentControl> _movementPathSegments = [];
    private readonly List<WeaponAttackControl> _weaponAttackControls = [];
    private readonly AvaloniaResourcesLocator _resourcesLocator = new();

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
        var localizationService = ViewModel?.LocalizationService;
        var bitmaskService = ViewModel?.TerrainBitmaskService;

        var hexDataList = new List<HexRenderData>();
        foreach (var hex in game.BattleMap?.GetHexes() ?? [])
        {
            var edges = game.BattleMap?.GetHexEdges(hex.Coordinates) ?? [];

            CanonicalBitmaskResult? waterBitmask = null;
            if (bitmaskService != null && game.BattleMap != null && hex.HasTerrain(MakaMekTerrains.Water))
            {
                waterBitmask =
                    bitmaskService.ComputeCanonicalBitmask(game.BattleMap, hex.Coordinates, MakaMekTerrains.Water);
            }

            CanonicalBitmaskResult? roadBitmask = null;
            if (bitmaskService != null && game.BattleMap != null &&
                (hex.HasTerrain(MakaMekTerrains.Road) || hex.HasTerrain(MakaMekTerrains.Bridge)))
            {
                var rawRoad = bitmaskService.ComputeRawBitmask(game.BattleMap, hex.Coordinates, MakaMekTerrains.Road,
                    (current, neighbor) => current.CanRoadConnectTo(neighbor));
                var rawBridge = bitmaskService.ComputeRawBitmask(game.BattleMap, hex.Coordinates,
                    MakaMekTerrains.Bridge, (current, neighbor) => current.CanRoadConnectTo(neighbor));
                roadBitmask = bitmaskService.CanonicalizeRawMask((byte)(rawRoad | rawBridge));
            }

            hexDataList.Add(new HexRenderData(
                hex, edges, waterBitmask, roadBitmask));

            if (hex.Coordinates.H > maxH) maxH = hex.Coordinates.H;
            if (hex.Coordinates.V > maxV) maxV = hex.Coordinates.V;
        }

        MapCanvas.SetHexData(
            hexDataList,
            hexConfiguration ?? HexRenderConfiguration.Default,
            game.Logger,
            terrainAssetService,
            localizationService,
            ViewModel?.Scheduler ?? System.Reactive.Concurrency.Scheduler.Default,
            _resourcesLocator);

        _unitControls = ViewModel?.Units
            .Select(u => new UnitControl(u, (IImageService<Bitmap>)ViewModel.ImageService, ViewModel))
            .ToList();
        if (_unitControls != null)
        {
            foreach (var unitControl in _unitControls)
            {
                MapCanvas.Children.Add(unitControl);
            }
        }

        // Ensure overlays stay on top
        MapCanvas.Children.Add(directionSelector);
        MapCanvas.Children.Add(SurfaceSelector);

        MapCanvas.Width = maxH + 2 * HexCoordinatesPixelExtensions.HexWidth;
        MapCanvas.Height =
            maxV + 3 * HexCoordinatesPixelExtensions.HexHeight;

        // restore overlays after a full canvas rebuild
        UpdateMovementPath();
        UpdateWeaponAttacks();
        UpdateHighlightBoundaryOutlines();
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

        // Handle SurfaceSelector interaction
        if (SurfaceSelector.IsVisible)
        {
            if (SurfaceSelector.Bounds.Contains(clickPosition))
            {
                var surfaceSelectorPosition = clickPosition - SurfaceSelector.Bounds.Position;
                if (SurfaceSelector.HandleInteraction(surfaceSelectorPosition)) return;
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

        // Hex selection via pixel→coordinate lookup
        if (ViewModel?.Game?.BattleMap != null)
        {
            var coords = HexCoordinatesPixelExtensions.FromPixel(clickPosition.X, clickPosition.Y);
            var hex = ViewModel.Game.BattleMap.GetHexes()
                .FirstOrDefault(h => h.Coordinates == coords);
            if (hex != null)
                ViewModel.HandleHexSelection(hex);
        }
    }

    protected override void OnViewModelSet()
    {
        base.OnViewModelSet();
        if (ViewModel is { Game: not null })
        {
            RenderMap(ViewModel.Game);
            ViewModel.CaptureMap = CaptureMapAsync;
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private async Task<(byte[] PngBytes, int WidthPixels, int HeightPixels)> CaptureMapAsync()
    {
        var pngBytes = await MapCanvas.ToPngAsync();
        return (pngBytes, (int)MapCanvas.Width, (int)MapCanvas.Height);
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
        else if (e.PropertyName == nameof(ViewModel.HighlightBoundaryOutlines))
        {
            UpdateHighlightBoundaryOutlines();
        }
        else if (e.PropertyName == nameof(ViewModel.HexConfiguration))
        {
            if (ViewModel?.HexConfiguration != null)
            {
                var config = ViewModel.HexConfiguration.ToConfiguration();
                MapCanvas.UpdateHexConfiguration(config);
            }
        }
    }

    private void UpdateHighlightBoundaryOutlines()
    {
        MapCanvas.SetBoundaryOutlines(
            ViewModel?.HighlightBoundaryOutlines);
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
        TurnInfoPanel.MaxWidth = Math.Max(e.NewSize.Width - 6, 300);
    }
}
