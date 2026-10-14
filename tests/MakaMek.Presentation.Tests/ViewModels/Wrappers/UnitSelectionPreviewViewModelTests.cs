using NSubstitute;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels.Wrappers;

public class UnitSelectionPreviewViewModelTests
{
    private sealed class TestWeapon(int elementaryDamage)
        : Weapon(new WeaponDefinition(
            "Test Weapon", elementaryDamage, 3,
            new WeaponRange(0, 3, 6, 9),
            WeaponType.Energy, 10));

    private static UnitData UnitData(
        string chassis = "Locust",
        string model = "LCT-1V",
        string? name = null,
        Dictionary<string, string>? additionalAttributes = null)
        => MechFactoryTests.CreateDummyMechData() with
        {
            Chassis = chassis,
            Model = model,
            Name = name,
            AdditionalAttributes = additionalAttributes ?? new Dictionary<string, string>()
        };

    private static IUnit Unit(params Weapon[] weapons)
    {
        var unit = Substitute.For<IUnit>();
        unit.GetAvailableWeapons().Returns(weapons);
        return unit;
    }

    [Fact]
    public void DisplayName_WhenNameIsSet_UsesIt()
        => new UnitSelectionPreviewViewModel(UnitData(name: "Old Reliable"), Unit())
            .DisplayName.ShouldBe("Old Reliable");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DisplayName_WhenNameIsMissing_FallsBackToChassisAndModel(string? name)
        => new UnitSelectionPreviewViewModel(UnitData(name: name), Unit())
            .DisplayName.ShouldBe("Locust LCT-1V");

    [Fact]
    public void WeightClass_ComesFromTheConstructedUnit()
    {
        var unit = Unit();
        unit.Class.Returns(Sanet.MakaMek.Core.Data.Units.WeightClass.Assault);

        new UnitSelectionPreviewViewModel(UnitData(), unit).WeightClass.ShouldBe("Assault");
    }

    [Fact]
    public void UnitFacts_AreProjectedFromTheConstructedUnit()
    {
        var unit = Unit();
        unit.Tonnage.Returns(55);
        unit.CalculateBattleValue().Returns(1234);
        unit.AvailableWalkingPoints.Returns(5);
        unit.AvailableRunningPoints.Returns(8);
        unit.AvailableJumpingPoints.Returns(5);
        unit.HeatDissipation.Returns(12);

        var sut = new UnitSelectionPreviewViewModel(UnitData(), unit);

        sut.Tonnage.ShouldBe(55);
        sut.BattleValue.ShouldBe(1234);
        sut.WalkingPoints.ShouldBe(5);
        sut.RunningPoints.ShouldBe(8);
        sut.JumpingPoints.ShouldBe(5);
        sut.MovementSummary.ShouldBe("5 / 8 / 5");
        sut.HeatDissipation.ShouldBe(12);
    }

    [Fact]
    public void ArmorAndStructure_ComeFromTheConstructedUnit()
    {
        var unit = Unit();
        unit.TotalMaxArmor.Returns(160);
        unit.TotalMaxStructure.Returns(90);

        var sut = new UnitSelectionPreviewViewModel(UnitData(), unit);

        sut.MaxArmor.ShouldBe(160);
        sut.MaxStructure.ShouldBe(90);
    }

    [Fact]
    public void Weapons_AreCountedAndTheirDamageSummed()
    {
        var sut = new UnitSelectionPreviewViewModel(
            UnitData(), Unit(new TestWeapon(5), new TestWeapon(10), new TestWeapon(3)));

        sut.WeaponCount.ShouldBe(3);
        sut.TotalWeaponDamage.ShouldBe(18);
    }

    [Fact]
    public void Weapons_AreReportedAsZeroWhenTheUnitIsUnarmed()
    {
        var sut = new UnitSelectionPreviewViewModel(UnitData(), Unit());

        sut.WeaponCount.ShouldBe(0);
        sut.TotalWeaponDamage.ShouldBe(0);
    }

    [Fact]
    public void UnitData_IsExposedUnchanged()
    {
        var data = UnitData(chassis: "Atlas", model: "AS7-D");

        new UnitSelectionPreviewViewModel(data, Unit()).UnitData.ShouldBe(data);
    }
}
