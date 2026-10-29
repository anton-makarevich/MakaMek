using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using MonacoLabs.Presto;
using Sanet.MakaMek.Avalonia.Services;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Avalonia.Controls;

/// <summary>
/// The badge that marks a declared physical attack: a small hex on the edge between the attacker and
/// its target, carrying a fist for a punch or a boot for a kick.
/// </summary>
/// <remarks>
/// It is inert, so it never steals a click from the map, and it owns its animations the way
/// <see cref="SelectionHex"/> does. The badge is created when a declaration arrives and disposed
/// when the attack resolves or the map is rebuilt, so the pulse never outlives what it marks.
/// </remarks>
internal sealed class PhysicalAttackIndicatorControl : Grid, IDisposable
{
    /// <summary>
    /// The badge is a fraction of a map hex: small enough to sit on a shared edge, large enough for
    /// the icon to read at map scale.
    /// </summary>
    public const double SizeFactor = 0.34;

    /// <summary>
    /// How far the badge slides along the shared edge, to the right of the attack direction.
    /// </summary>
    /// <remarks>
    /// Two units can declare an attack on each other, and both declarations sit on the same edge.
    /// Shifting each badge to the right of its own attack direction puts the two on opposite halves
    /// of the edge instead of on top of each other, the same way weapon declarations offset their
    /// lines.
    /// </remarks>
    public const double EdgeShift = 18;

    /// <summary>
    /// Above the unit controls, which sit at one, and below the raised action buttons, which take
    /// the canvas child count.
    /// </summary>
    public const int LayerIndex = 2;

    /// <summary>
    /// How far the glyph is tilted, in degrees clockwise, around its own centre. The hex stays
    /// upright; only the glyph inside is turned, so it reads as an attack rather than a straight-on
    /// badge and the boot points the way a kick travels.
    /// </summary>
    public const double IconRotationDegrees = 35;

    /// <summary>
    /// How far the badge hex is pulled in from its edge, in unscaled map-hex pixels. At the badge
    /// scale that is about half a pixel, which is all the hex needs to keep its stroke on the path
    /// edge instead of well inside it.
    /// </summary>
    private const double BadgeInset = 1.5;
    private const double IconSize = 17;
    private const double BadgeStrokeThickness = 2;

    private readonly Path _pulse;
    private readonly Path _badge;
    private readonly Path _icon;
    private readonly ControlScope _scope;
    private Animation? _enterAnimation;
    private Animation? _pulseAnimation;

    public PhysicalAttackIndicatorControl(PhysicalAttackIndicatorViewModel viewModel)
    {
        ViewModel = viewModel;

        Width = HexCoordinatesPixelExtensions.HexWidth * SizeFactor;
        Height = HexCoordinatesPixelExtensions.HexHeight * SizeFactor;
        IsHitTestVisible = false;
        ZIndex = LayerIndex;
        RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);

        var tint = new SolidColorBrush(Color.Parse(viewModel.AttackerTint));
        _pulse = HexShapeMotion.CreateScaledHex(SizeFactor, 0, BadgeStrokeThickness, tint);
        _badge = HexShapeMotion.CreateScaledHex(SizeFactor, BadgeInset, BadgeStrokeThickness, tint);
        _icon = CreateIcon(viewModel.AttackType, tint);

        Children.Add(_pulse);
        Children.Add(_badge);
        Children.Add(_icon);

        var center = IndicatorCenter(viewModel.From, viewModel.To);
        Canvas.SetLeft(this, center.X - Width / 2);
        Canvas.SetTop(this, center.Y - Height / 2);

        // The badge lives in its own scope, so every animation it starts dies with it.
        _scope = PrestoAvalonia.CreateScope(this);
        _scope.Execute(_ =>
        {
            _enterAnimation = this.Animate(HexShapeMotion.IndicatorEnter);
            // Only the pulse path loops: the badge and the icon stay where the entrance left them.
            // The pulse is the unit selector's, so a declaration reads as the same kind of marker.
            _pulseAnimation = _pulse.Animate(HexShapeMotion.IdlePulse);
        });
    }

    public PhysicalAttackIndicatorViewModel ViewModel { get; }

    /// <summary>The looping hex that draws attention to the badge.</summary>
    internal Path PulsePath => _pulse;

    /// <summary>The static hex that carries the icon.</summary>
    internal Path BadgePath => _badge;

    /// <summary>The punch or kick glyph.</summary>
    internal Path IconPath => _icon;

    /// <summary>
    /// Whether this badge already shows the same declaration. The map uses it to keep a running
    /// badge instead of rebuilding it when only the pending limbs change.
    /// </summary>
    public bool Shows(PhysicalAttackIndicatorViewModel viewModel) =>
        ViewModel.AttackerId == viewModel.AttackerId
        && ViewModel.TargetId == viewModel.TargetId
        && ViewModel.AttackType == viewModel.AttackType
        && ViewModel.From == viewModel.From
        && ViewModel.To == viewModel.To
        && ViewModel.AttackerTint == viewModel.AttackerTint;

    /// <summary>
    /// The centre of the badge in map pixels: the middle of the shared edge, shifted to the right of
    /// the attack direction so mutual declarations do not overlap.
    /// </summary>
    internal static Point IndicatorCenter(HexCoordinates from, HexCoordinates to)
    {
        var fromCenter = HexCenter(from);
        var toCenter = HexCenter(to);
        var midpoint = new Point((fromCenter.X + toCenter.X) / 2, (fromCenter.Y + toCenter.Y) / 2);

        var dx = toCenter.X - fromCenter.X;
        var dy = toCenter.Y - fromCenter.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < double.Epsilon) return midpoint;

        // Pixel coordinates grow downwards, so the right of the attack direction is (-dy, dx).
        return new Point(
            midpoint.X - dy / length * EdgeShift,
            midpoint.Y + dx / length * EdgeShift);
    }

    /// <summary>
    /// The resource key of the icon for an attack type. Punch, Kick and Push each have their own
    /// icon so the declaration badge is unambiguous; anything else falls back to the punch icon
    /// rather than drawing nothing.
    /// </summary>
    internal static string IconResourceKey(PhysicalAttackType attackType) => attackType switch
    {
        PhysicalAttackType.Kick => "KickIcon",
        PhysicalAttackType.Push => "PushIcon",
        _ => "PunchIcon"
    };

    private static Point HexCenter(HexCoordinates coordinates) => new(
        coordinates.H + HexCoordinatesPixelExtensions.HexWidth * 0.5,
        coordinates.V + HexCoordinatesPixelExtensions.HexHeight * 0.5);

    /// <summary>
    /// Builds the attack-type glyph, filled with the attacker tint so the badge reads as the
    /// owner's mark the way the hex and the pulse do.
    /// </summary>
    private static Path CreateIcon(PhysicalAttackType attackType, IBrush brush)
    {
        var resources = new AvaloniaResourcesLocator();
        return new Path
        {
            Data = resources.TryFindResource(IconResourceKey(attackType)) as Geometry,
            Fill = brush,
            Width = IconSize,
            Height = IconSize,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            // The glyph never animates, so it owns its render transform; Presto only ever touches
            // the badge and the pulse, and the hex outline stays upright.
            RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            RenderTransform = new RotateTransform(IconRotationDegrees),
            IsHitTestVisible = false
        };
    }

    public void Dispose()
    {
        _pulseAnimation?.Revert();
        _pulseAnimation = null;
        _enterAnimation?.Cancel();
        _enterAnimation = null;
        _scope.Revert();
    }
}
