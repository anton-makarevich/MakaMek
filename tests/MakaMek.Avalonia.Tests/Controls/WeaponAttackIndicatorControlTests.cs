using Avalonia;
using Avalonia.Headless;
using Avalonia.Media;
using NSubstitute;
using Sanet.MakaMek.Avalonia.Controls;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Controls;

/// <summary>
/// Units can share a hex after a blocked domino displacement, so a weapon line between them has no
/// direction. The control must draw nothing instead of dividing by a zero length.
/// </summary>
public class WeaponAttackIndicatorControlTests
{
    [Fact]
    public void HasDrawableLength_IsFalseForCoLocatedEndpoints()
    {
        var point = new Point(10, 10);

        WeaponAttackIndicatorControl.HasDrawableLength(point, point).ShouldBeFalse();
        WeaponAttackIndicatorControl.HasDrawableLength(point, new Point(20, 10)).ShouldBeTrue();
    }

    [Fact]
    public Task CoLocatedEndpointsDoNotThrowWhileRendering() => Dispatch(() =>
    {
        var control = new WeaponAttackIndicatorControl(new WeaponAttackIndicatorViewModel
        {
            From = new HexCoordinates(1, 1),
            To = new HexCoordinates(1, 1),
            Weapon = new MediumLaser(),
            AttackerTint = "#FF0000",
            LineOffset = 5,
            TargetId = Guid.NewGuid()
        });
        var context = Substitute.For<DrawingContext>();

        Should.NotThrow(() => control.Render(context));
    });

    private static Task Dispatch(Action action)
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(
            typeof(WeaponAttackIndicatorControlTests).Assembly);
        return session.Dispatch(action, CancellationToken.None);
    }
}
