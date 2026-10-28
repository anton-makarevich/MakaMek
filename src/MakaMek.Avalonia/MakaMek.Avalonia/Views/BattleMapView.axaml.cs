using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Avalonia.Controls;
using Sanet.MakaMek.Avalonia.Services;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Map.Data;
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
    private readonly Dictionary<Guid, PhysicalAttackIndicatorControl> _physicalAttackIndicators = [];
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
        // The badges loop, so they have to die with the canvas they are drawn on.
        DisposePhysicalAttackIndicators();
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
            HexRenderData hexData;
            if (bitmaskService != null && game.BattleMap != null)
            {
                hexData = bitmaskService.CreateHexRenderData(game.BattleMap, hex.Coordinates);
            }
            else
            {
                var edges = game.BattleMap?.GetHexEdges(hex.Coordinates) ?? [];
                hexData = new HexRenderData(hex, edges, null, null);
            }

            hexDataList.Add(hexData);

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

        // The map is rebuilt from scratch, so the previous unit controls have to go: their animations
        // and their poll subscription would otherwise outlive the controls they belong to.
        DisposeUnitControls();

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
        UpdatePhysicalAttackIndicators();
    }

    /// <summary>
    /// Disposes the unit controls of the previous map render.
    /// </summary>
    private void DisposeUnitControls()
    {
        if (_unitControls is not { } unitControls) return;

        foreach (var unitControl in unitControls)
        {
            unitControl.Dispose();
        }

        _unitControls = null;
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
        ViewModel?.SelectHexAt(clickPosition.X, clickPosition.Y);
    }

    protected override void OnViewModelSet()
    {
        base.OnViewModelSet();
        if (ViewModel == null) return;
        ViewModel.CaptureMap = CaptureViewMap;
        ViewModel.CenterMap = () => MapCanvas.CenterMap();
        // A unit that has not deployed has no position, so there is nothing to center on.
        ViewModel.FocusUnit = unit =>
        {
            if (unit.Position is { } position) MapCanvas.CenterOnHex(position.Coordinates);
        };
        if (ViewModel.Game is not { } game) return;
        RenderMap(game);
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private async Task<(byte[] PngBytes, int WidthPixels, int HeightPixels)> CaptureViewMap()
    {
        var pngBytes = await MapCanvas.ToPng();
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
        else if (e.PropertyName == nameof(ViewModel.PhysicalAttackIndicators))
        {
            UpdatePhysicalAttackIndicators();
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

    /// <summary>
    /// Keeps the attack badges in step with the view model without rebuilding the ones that are
    /// still running: a rebuilt badge would restart its entrance and pulse every time a limb
    /// resolves.
    /// </summary>
    private void UpdatePhysicalAttackIndicators()
    {
        var indicators = ViewModel?.PhysicalAttackIndicators;
        if (indicators is not { Count: > 0 })
        {
            DisposePhysicalAttackIndicators();
            return;
        }

        foreach (var attackerId in _physicalAttackIndicators.Keys
                     .Where(id => indicators.All(indicator => indicator.AttackerId != id))
                     .ToList())
        {
            RemovePhysicalAttackIndicator(attackerId);
        }

        foreach (var indicator in indicators)
        {
            if (_physicalAttackIndicators.TryGetValue(indicator.AttackerId, out var existing))
            {
                if (existing.Shows(indicator)) continue;
                RemovePhysicalAttackIndicator(indicator.AttackerId);
            }

            var control = new PhysicalAttackIndicatorControl(indicator);
            _physicalAttackIndicators[indicator.AttackerId] = control;
            MapCanvas.Children.Add(control);
        }
    }

    private void RemovePhysicalAttackIndicator(Guid attackerId)
    {
        if (!_physicalAttackIndicators.Remove(attackerId, out var control)) return;
        MapCanvas.Children.Remove(control);
        control.Dispose();
    }

    private void DisposePhysicalAttackIndicators()
    {
        foreach (var control in _physicalAttackIndicators.Values)
        {
            MapCanvas.Children.Remove(control);
            control.Dispose();
        }

        _physicalAttackIndicators.Clear();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // A view that is attached again gets its badges back; the declarations they mark may still
        // be pending if the view was detached mid-phase.
        UpdatePhysicalAttackIndicators();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // The badges loop, and Presto only pauses the animations of a detached control by default,
        // which keeps them alive and ticking against a visual tree that is gone.
        DisposePhysicalAttackIndicators();
        base.OnDetachedFromVisualTree(e);
    }
}
