using System.Globalization;
using Sanet.MakaMek.Avalonia.Converters;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Shouldly;
using MakaMek.Avalonia.Tests.TestHelpers;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;

namespace MakaMek.Avalonia.Tests.Converters;

public class WeaponRangeConverterTests
{
    private readonly WeaponRangeConverter _sut = new();

    [Theory]
    [InlineData(0, 6, 12, 18, "-|6|12|18")]  // No minimum range
    [InlineData(2, 6, 12, 18, "2|6|12|18")]  // With minimum range
    public void Convert_ValidWeapon_ReturnsFormattedRangeString(
        int minRange, int shortRange, int mediumRange, int longRange, string expected)
    {
        // Arrange
        var weapon = new TestWeapon(
            minimumRange: minRange,
            shortRange: shortRange,
            mediumRange: mediumRange,
            longRange: longRange);

        // Act
        var result = _sut.Convert(weapon, typeof(string), null, CultureInfo.InvariantCulture) as string;

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void Convert_NullWeapon_ReturnsNull()
    {
        // Act
        var result = _sut.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Convert_NonWeaponObject_ReturnsNull()
    {
        // Act
        var result = _sut.Convert("not a weapon", typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Convert_WeaponWithNullRange_ReturnsNull()
    {
        // Arrange
        var coordinates = new HexCoordinates(1, 1);
        var hex = new Hex(coordinates);
        hex.AddTerrain(new WaterTerrain(-2));
        var centerTorso = new CenterTorso("Torso", 12, 2, 12);
        var mech = new Mech("Test", "TST-1", 20, [centerTorso]);
        var weapon = new NullRangeWeapon();
        centerTorso.TryAddComponent(weapon).ShouldBeTrue();
        mech.Deploy(new HexPosition(coordinates, HexDirection.Bottom), hex);
        weapon.IsSubmerged.ShouldBeTrue();

        // Act
        var result = _sut.Convert(weapon, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ConvertBack_ThrowsNotImplementedException()
    {
        // Act & Assert
        Should.Throw<NotImplementedException>(() =>
            _sut.ConvertBack(null, typeof(Weapon), null, CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// Test weapon that simulates a null range (e.g. submerged weapon without underwater range)
/// </summary>
public class NullRangeWeapon() : Weapon(new WeaponDefinition(
    Name: "Null Range Weapon",
    ElementaryDamage: 1,
    Heat: 1,
    Range: new WeaponRange(0, 6, 12, 18),
    Type: WeaponType.Energy,
    BattleValue: 1,
    UnderwaterRange: null,
    WeaponComponentType: MakaMekComponent.MachineGun));