using System;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using MonacoLabs.Presto;
using Sanet.MakaMek.Presentation.UiStates;

namespace Sanet.MakaMek.Avalonia.Controls;

/// <summary>
/// The hexagonal outline that marks the selected unit, and the motion that plays it.
/// </summary>
/// <remarks>
/// The outline is three stacked paths: the hexagon itself, a one-shot sonar echo and a pulse that
/// loops while the unit stays selected. It owns its animations and their lifetime, so the unit
/// control only has to tell it which role the unit plays.
/// </remarks>
internal sealed class SelectionHex : Grid, IDisposable
{
    private readonly Path _hex;
    private readonly Path _echo;
    private readonly Path _pulse;
    private readonly ControlScope _scope;
    private readonly IBrush _ownerBrush;
    private readonly IBrush _targetBrush;
    private Animation? _hexAnimation;
    private Animation? _echoAnimation;
    private Animation? _pulseAnimation;
    private UnitSelectionKind _kind = UnitSelectionKind.None;

    /// <summary>
    /// Creates the outline for a unit.
    /// </summary>
    /// <param name="ownerBrush">The brush of the own units.</param>
    /// <param name="targetBrush">The brush of a hostile weapon attack target.</param>
    public SelectionHex(IBrush ownerBrush, IBrush targetBrush)
    {
        _ownerBrush = ownerBrush;
        _targetBrush = targetBrush;

        IsHitTestVisible = false;

        // The outline lives in its own scope, so every animation it starts dies with it.
        _scope = PrestoAvalonia.CreateScope(this);
        _hex = HexShapeMotion.CreateHex(HexShapeMotion.Inset, 4, ownerBrush);
        _echo = HexShapeMotion.CreateHex(HexShapeMotion.Inset, 2, ownerBrush);
        _pulse = HexShapeMotion.CreateHex(HexShapeMotion.Inset, 2, ownerBrush);

        // The hexagon draws its own stroke, so it starts hidden until the entrance reveals it.
        PrestoAvalonia.CreateDrawable(_hex);

        Children.Add(_pulse);
        Children.Add(_echo);
        Children.Add(_hex);
    }

    /// <summary>
    /// Shows the outline for the role the unit plays.
    /// </summary>
    /// <remarks>
    /// Called from the poll, so it edge-detects the role and only starts animations when the role
    /// actually changes - restarting them on every tick would restart them on every tick.
    /// </remarks>
    public void Update(UnitSelectionKind kind)
    {
        if (kind == _kind) return;

        var previousKind = _kind;
        _kind = kind;

        if (kind == UnitSelectionKind.None)
        {
            StopPulse();
            // Cancel keeps the hexagon where it is, so the fade out starts from the current state.
            _hexAnimation?.Cancel();
            _hexAnimation = null;
            _echoAnimation?.Revert();
            _echoAnimation = null;
            _scope.Execute(_ => { _hexAnimation = _hex.Animate(HexShapeMotion.Exit); });
            return;
        }

        ApplyStyle(kind);

        if (previousKind == UnitSelectionKind.None)
        {
            // Only the entrance draws the stroke in; a role change keeps the hexagon as it is.
            _hexAnimation?.Cancel();
            _hexAnimation = null;
            _echoAnimation?.Revert();
            _echoAnimation = null;
            _scope.Execute(_ =>
            {
                _hexAnimation = _hex.Animate(HexShapeMotion.Enter);
                _echoAnimation = _echo.Animate(HexShapeMotion.Echo);
            });
        }

        StopPulse();
        if (kind == UnitSelectionKind.Attacker) return;

        var pulse = kind == UnitSelectionKind.Target
            ? HexShapeMotion.TargetPulse
            : HexShapeMotion.IdlePulse;
        _scope.Execute(_ => { _pulseAnimation = _pulse.Animate(pulse); });
    }

    /// <summary>
    /// Applies the brush and the stroke width that belong to the role.
    /// </summary>
    private void ApplyStyle(UnitSelectionKind kind)
    {
        var brush = kind == UnitSelectionKind.Target ? _targetBrush : _ownerBrush;
        _hex.Stroke = brush;
        _echo.Stroke = brush;
        _pulse.Stroke = brush;
        _hex.StrokeThickness = kind == UnitSelectionKind.Target ? 5 : 4;
        _echo.StrokeThickness = kind == UnitSelectionKind.Target ? 3 : 2;
        _pulse.StrokeThickness = kind == UnitSelectionKind.Target ? 3 : 2;
    }

    /// <summary>
    /// Stops the pulse and puts the pulse path back to its resting state.
    /// </summary>
    private void StopPulse()
    {
        _pulseAnimation?.Revert();
        _pulseAnimation = null;
    }

    public void Dispose()
    {
        StopPulse();
        _hexAnimation?.Cancel();
        _echoAnimation?.Revert();
        _hexAnimation = null;
        _echoAnimation = null;
        _scope.Revert();
    }
}
