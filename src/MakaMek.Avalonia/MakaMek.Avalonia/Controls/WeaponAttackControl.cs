using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Presentation.ViewModels;

namespace Sanet.MakaMek.Avalonia.Controls;

public class WeaponAttackControl : Control
{
    private readonly WeaponAttackViewModel _viewModel;
    private readonly Point _from;
    private readonly Point _to;
    private readonly IBrush _color;
    
    public WeaponAttackControl(WeaponAttackViewModel viewModel)
    {
        _viewModel = viewModel;
        _from = new Point(viewModel.From.H + HexCoordinates.HexWidth * 0.5, viewModel.From.V + HexCoordinates.HexHeight * 0.5);
        _to = new Point(viewModel.To.H + HexCoordinates.HexWidth * 0.5, viewModel.To.V + HexCoordinates.HexHeight * 0.5);
        _color = new SolidColorBrush(Color.Parse(viewModel.AttackerTint));
    }

    private IDashStyle? GetDashStyleForWeapon()
    {
        var weaponType = _viewModel.Weapon.Type;
        return weaponType switch
        {
            WeaponType.Energy => null,
            WeaponType.Missile => DashStyle.Dash,
            WeaponType.Ballistic => DashStyle.DashDotDot,
            _ => null // Default to solid
        };
    }

    public override void Render(DrawingContext context)
    {
        var from = _from;
        var to = _to;
        
        // Apply offset perpendicular to the line direction
        if (_viewModel.LineOffset != 0)
        {
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            var offsetX = (-dy / length) * _viewModel.LineOffset;
            var offsetY = (dx / length) * _viewModel.LineOffset;
            
            from = from + new Point(offsetX, offsetY);
            to = to + new Point(offsetX, offsetY);
        }

        // Draw arrow line with weapon-specific style
        var pen = new Pen(_color, 2, dashStyle: GetDashStyleForWeapon());
        context.DrawLine(pen, from, to);

        // Draw arrowhead
        DrawArrowhead(context, from, to);
    }

    private void DrawArrowhead(DrawingContext context, Point start, Point end)
    {
        const double arrowSize = 10;
        const double arrowAngle = Math.PI / 6; // 30 degrees

        var direction = end - start;
        var length = Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
        var normalizedDirection = new Point(direction.X / length, direction.Y / length);

        var angle1 = Math.Atan2(normalizedDirection.Y, normalizedDirection.X) + Math.PI - arrowAngle;
        var angle2 = Math.Atan2(normalizedDirection.Y, normalizedDirection.X) + Math.PI + arrowAngle;

        var arrowPoint1 = new Point(
            end.X + arrowSize * Math.Cos(angle1),
            end.Y + arrowSize * Math.Sin(angle1));
        var arrowPoint2 = new Point(
            end.X + arrowSize * Math.Cos(angle2),
            end.Y + arrowSize * Math.Sin(angle2));

        var geometry = new StreamGeometry();
        using (var context2 = geometry.Open())
        {
            context2.BeginFigure(end, true);
            context2.LineTo(arrowPoint1);
            context2.LineTo(arrowPoint2);
        }

        context.DrawGeometry(_color, null, geometry);
    }
}
