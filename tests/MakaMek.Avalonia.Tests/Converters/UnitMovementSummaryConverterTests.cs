using System.Globalization;
using NSubstitute;
using Sanet.MakaMek.Avalonia.Converters;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Localization;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Converters;

public class UnitMovementSummaryConverterTests
{
    [Fact]
    public void Convert_UsesRulesCalculatedRemainingMovement()
    {
        var unit = Substitute.For<IUnit>();
        unit.GetMovementPoints(MovementType.Walk).Returns(4);
        unit.GetMovementPoints(MovementType.Run).Returns(6);

        var localization = Substitute.For<ILocalizationService>();
        localization.GetString("UnitHud_MovementSummary").Returns("W{0}/R{1} MP");
        var result = new UnitMovementSummaryConverter(localization).Convert(unit, typeof(string), null, CultureInfo.InvariantCulture);

        result.ShouldBe("W4/R6 MP");
        unit.Received(1).GetMovementPoints(MovementType.Walk);
        unit.Received(1).GetMovementPoints(MovementType.Run);
    }
}
