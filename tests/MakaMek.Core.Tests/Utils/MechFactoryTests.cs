using NSubstitute;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Ballistic;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Melee;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Utils;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Utils;

public class MechFactoryTests
{
    private readonly MechFactory _mechFactory;
    private readonly UnitData _unitData;

    public MechFactoryTests()
    {
        var structureValueProvider = Substitute.For<IRulesProvider>();
        structureValueProvider.GetStructureValues(20).Returns(new Dictionary<PartLocation, int>
        {
            { PartLocation.Head, 8 },
            { PartLocation.CenterTorso, 10 },
            { PartLocation.LeftTorso, 8 },
            { PartLocation.RightTorso, 8 },
            { PartLocation.LeftArm, 4 },
            { PartLocation.RightArm, 4 },
            { PartLocation.LeftLeg, 8 },
            { PartLocation.RightLeg, 8 }
        });
        _unitData = CreateDummyMechData();
        _mechFactory = new MechFactory(structureValueProvider, Substitute.For<ILocalizationService>());
    }

    public static UnitData CreateDummyMechData(Tuple<PartLocation, List<MakaMekComponent>>? locationEquipment = null)
    {
        var data = new UnitData
        {
            Chassis = "Locust",
            Model = "LCT-1V",
            Mass = 20,
            WalkMp = 8,
            EngineRating = 275,
            EngineType = "Fusion",
            ArmorValues = new Dictionary<PartLocation, ArmorLocation>
            {
                { PartLocation.Head, new ArmorLocation { FrontArmor = 8 } },
                { PartLocation.CenterTorso, new ArmorLocation { FrontArmor = 10, RearArmor = 5 } },
                { PartLocation.LeftTorso, new ArmorLocation { FrontArmor = 8, RearArmor = 4 } },
                { PartLocation.RightTorso, new ArmorLocation { FrontArmor = 8, RearArmor = 4 } },
                { PartLocation.LeftArm, new ArmorLocation { FrontArmor = 4 } },
                { PartLocation.RightArm, new ArmorLocation { FrontArmor = 4 } },
                { PartLocation.LeftLeg, new ArmorLocation { FrontArmor = 8 } },
                { PartLocation.RightLeg, new ArmorLocation { FrontArmor = 8 } }
            },
            LocationEquipment = new Dictionary<PartLocation, List<MakaMekComponent>>
            {
                { PartLocation.LeftArm, [MakaMekComponent.MachineGun] },
                { PartLocation.RightLeg, [MakaMekComponent.ISAmmoMG] },
                { PartLocation.CenterTorso, [MakaMekComponent.Engine] },
                { PartLocation.RightArm, [MakaMekComponent.UpperArmActuator, MakaMekComponent.MediumLaser] }
            },
            Quirks = new Dictionary<string, string>(),
            AdditionalAttributes = new Dictionary<string, string>()
        };
        if (locationEquipment != null)
        {
            data.LocationEquipment[locationEquipment.Item1] = locationEquipment.Item2;
        }
        return data;
    }

    [Fact]
    public void Create_CreatesCorrectMech()
    {
        // Act
        var mech = _mechFactory.Create(_unitData);

        // Assert
        mech.Chassis.ShouldBe("Locust");
        mech.Model.ShouldBe("LCT-1V");
        mech.Name.ShouldBe("Locust LCT-1V");
        mech.Tonnage.ShouldBe(20);
        mech.Class.ShouldBe(WeightClass.Light);
        mech.GetMovementPoints(MovementType.Walk).ShouldBe(8);
    }

    [Fact]
    public void Create_HasCorrectArmor()
    {
        // Act
        var mech = _mechFactory.Create(_unitData);

        // Assert
        mech.Parts[PartLocation.LeftArm].CurrentArmor.ShouldBe(4);
        mech.Parts[PartLocation.RightArm].CurrentArmor.ShouldBe(4);
        mech.Parts[PartLocation.LeftTorso].CurrentArmor.ShouldBe(8);
        mech.Parts[PartLocation.RightTorso].CurrentArmor.ShouldBe(8);
        mech.Parts[PartLocation.CenterTorso].CurrentArmor.ShouldBe(10);
        mech.Parts[PartLocation.Head].CurrentArmor.ShouldBe(8);
        mech.Parts[PartLocation.LeftLeg].CurrentArmor.ShouldBe(8);
        mech.Parts[PartLocation.RightLeg].CurrentArmor.ShouldBe(8);
    }

    [Fact]
    public void Create_HasCorrectWeapons()
    {
        // Act
        var mech = _mechFactory.Create(_unitData);

        // Assert
        // Left Arm
        var leftArm = mech.Parts[PartLocation.LeftArm];
        leftArm.GetComponents<Weapon>().ShouldContain(w => w.Name == "Machine Gun");
        
        // Right Arm
        var rightArm = mech.Parts[PartLocation.RightArm];
        rightArm.GetComponents<Weapon>().ShouldContain(w => w.Name == "Medium Laser");
        
        // Center Torso
        var centerTorso = mech.Parts[PartLocation.CenterTorso];
        var engines = centerTorso.GetComponents<Engine>().ToList();
        engines.ShouldHaveSingleItem();
        engines[0].Rating.ShouldBe(275);
        engines[0].Type.ShouldBe(EngineType.Fusion);

    }

    [Fact]
    public void Create_HasCorrectActuators()
    {

        // Act
        var mech = _mechFactory.Create(_unitData);

        // Assert
        var leftArm = mech.Parts[PartLocation.LeftArm];
        leftArm.GetComponents<Component>().ShouldContain(a => a.Name == "Shoulder");

        var rightArm = mech.Parts[PartLocation.RightArm];
        rightArm.GetComponents<Component>().ShouldContain(a => a.Name == "Shoulder");
        rightArm.GetComponents<Component>().ShouldContain(a => a.Name == "Upper Arm Actuator");
    }
    
    [Fact]
    public void Create_CorrectlyAddsOneComponentThatOccupiesSeveralSlots()
    {
        // Arrange
        var locationEquipment = Tuple.Create(PartLocation.LeftTorso, new List<MakaMekComponent> 
        { 
            MakaMekComponent.AC5,
            MakaMekComponent.AC5,
            MakaMekComponent.AC5,
            MakaMekComponent.AC5 
        });
        var mechData = CreateDummyMechData(locationEquipment);

        // Act
        var mech = _mechFactory.Create(mechData);

        // Assert
        var leftTorso = mech.Parts[PartLocation.LeftTorso];
        var weapon = leftTorso.GetComponents<Ac5>();
        weapon.Count().ShouldBe(1); 
    }
    [Fact]
    public void Create_CorrectlyAddsTwoComponentsThatOccupySeveralSlots()
    {
        // Arrange
        var locationEquipment = Tuple.Create(PartLocation.LeftTorso, new List<MakaMekComponent> 
        { 
            MakaMekComponent.AC5,
            MakaMekComponent.AC5,
            MakaMekComponent.AC5,
            MakaMekComponent.AC5,
            MakaMekComponent.AC5,
            MakaMekComponent.AC5,
            MakaMekComponent.AC5,
            MakaMekComponent.AC5 
        });
        var mechData = CreateDummyMechData(locationEquipment);

        // Act
        var mech = _mechFactory.Create(mechData);

        // Assert
        var leftTorso = mech.Parts[PartLocation.LeftTorso];
        var weapon = leftTorso.GetComponents<Ac5>();
        weapon.Count().ShouldBe(2);
    }
    
    [Fact]
    public void Create_WithSmallLaser_ReturnsCorrectWeapon()
    {
        // Arrange
        var locationEquipment = Tuple.Create(PartLocation.RightArm, new List<MakaMekComponent> { MakaMekComponent.SmallLaser });
        var mechData = CreateDummyMechData(locationEquipment);

        // Act
        var mech = _mechFactory.Create(mechData);

        // Assert
        var rightArm = mech.Parts[PartLocation.RightArm];
        var weapon = rightArm.GetComponents<SmallLaser>().ShouldHaveSingleItem();
        weapon.Name.ShouldBe("Small Laser");
        weapon.Damage.ShouldBe(3);
        weapon.Heat.ShouldBe(1);
        weapon.ShortRange.ShouldBe(1);
        weapon.MediumRange.ShouldBe(2);
        weapon.LongRange.ShouldBe(3);
    }

    [Fact]
    public void Create_WithLargeLaser_ReturnsCorrectWeapon()
    {
        // Arrange
        var locationEquipment = Tuple.Create(PartLocation.RightTorso, new List<MakaMekComponent>
        {
            MakaMekComponent.LargeLaser,
            MakaMekComponent.LargeLaser
        });
        var mechData = CreateDummyMechData(locationEquipment);

        // Act
        var mech = _mechFactory.Create(mechData);

        // Assert
        var rightTorso = mech.Parts[PartLocation.RightTorso];
        var weapon = rightTorso.GetComponents<LargeLaser>().ShouldHaveSingleItem();
        weapon.Name.ShouldBe("Large Laser");
        weapon.Damage.ShouldBe(8);
        weapon.Heat.ShouldBe(8);
        weapon.ShortRange.ShouldBe(5);
        weapon.MediumRange.ShouldBe(10);
        weapon.LongRange.ShouldBe(15);
        weapon.Size.ShouldBe(2);
    }

    [Fact]
    public void Create_WithPPC_ReturnsCorrectWeapon()
    {
        // Arrange
        var locationEquipment = Tuple.Create(PartLocation.RightTorso, new List<MakaMekComponent>
        {
            MakaMekComponent.PPC,
            MakaMekComponent.PPC,
            MakaMekComponent.PPC
        });
        var mechData = CreateDummyMechData(locationEquipment);

        // Act
        var mech = _mechFactory.Create(mechData);

        // Assert
        var rightTorso = mech.Parts[PartLocation.RightTorso];
        var weapon = rightTorso.GetComponents<Ppc>().ShouldHaveSingleItem();
        weapon.Name.ShouldBe("PPC");
        weapon.Damage.ShouldBe(10);
        weapon.Heat.ShouldBe(10);
        weapon.MinimumRange.ShouldBe(3);
        weapon.ShortRange.ShouldBe(6);
        weapon.MediumRange.ShouldBe(12);
        weapon.LongRange.ShouldBe(18);
        weapon.Size.ShouldBe(3);
    }

    [Fact]
    public void Create_WithFlamer_ReturnsCorrectWeapon()
    {
        // Arrange
        var locationEquipment = Tuple.Create(PartLocation.LeftArm, new List<MakaMekComponent> { MakaMekComponent.Flamer });
        var mechData = CreateDummyMechData(locationEquipment);

        // Act
        var mech = _mechFactory.Create(mechData);

        // Assert
        var leftArm = mech.Parts[PartLocation.LeftArm];
        var weapon = leftArm.GetComponents<Flamer>().ShouldHaveSingleItem();
        weapon.Name.ShouldBe("Flamer");
        weapon.Damage.ShouldBe(2);
        weapon.Heat.ShouldBe(3);
        weapon.ShortRange.ShouldBe(1);
        weapon.MediumRange.ShouldBe(2);
        weapon.LongRange.ShouldBe(3);
    }
    
    [Fact]
    public void Create_AC2_ReturnsCorrectWeapon()
    {
        // Arrange
        var locationEquipment = Tuple.Create(PartLocation.RightTorso, new List<MakaMekComponent> { MakaMekComponent.AC2 });
        var mechData = CreateDummyMechData(locationEquipment);

        // Act
        var mech = _mechFactory.Create(mechData);

        // Assert
        var rightTorso = mech.Parts[PartLocation.RightTorso];
        var weapon = rightTorso.GetComponents<Ac2>().ShouldHaveSingleItem();
        weapon.Name.ShouldBe("AC/2");
        weapon.Damage.ShouldBe(2);
        weapon.Heat.ShouldBe(1);
        weapon.MinimumRange.ShouldBe(4);
        weapon.ShortRange.ShouldBe(8);
        weapon.MediumRange.ShouldBe(16);
        weapon.LongRange.ShouldBe(24);
    }

    [Fact]
    public void Create_WithAC10_ReturnsCorrectWeapon()
    {
        // Arrange
        var locationEquipment = Tuple.Create(PartLocation.RightTorso, new List<MakaMekComponent>
        {
            MakaMekComponent.AC10,
            MakaMekComponent.AC10,
            MakaMekComponent.AC10,
            MakaMekComponent.AC10,
            MakaMekComponent.AC10,
            MakaMekComponent.AC10,
            MakaMekComponent.AC10
        });
        var mechData = CreateDummyMechData(locationEquipment);

        // Act
        var mech = _mechFactory.Create(mechData);

        // Assert
        var rightTorso = mech.Parts[PartLocation.RightTorso];
        var weapon = rightTorso.GetComponents<Ac10>().ShouldHaveSingleItem();
        weapon.Name.ShouldBe("AC/10");
        weapon.Damage.ShouldBe(10);
        weapon.Heat.ShouldBe(3);
        weapon.MinimumRange.ShouldBe(0);
        weapon.ShortRange.ShouldBe(5);
        weapon.MediumRange.ShouldBe(10);
        weapon.LongRange.ShouldBe(15);
        weapon.Size.ShouldBe(7);
    }

    [Fact]
    public void Create_WithAC20_ReturnsCorrectWeapon()
    {
        // Arrange
        var locationEquipment = Tuple.Create(PartLocation.RightTorso, new List<MakaMekComponent>
        {
            MakaMekComponent.AC20,
            MakaMekComponent.AC20,
            MakaMekComponent.AC20,
            MakaMekComponent.AC20,
            MakaMekComponent.AC20,
            MakaMekComponent.AC20,
            MakaMekComponent.AC20,
            MakaMekComponent.AC20,
            MakaMekComponent.AC20,
            MakaMekComponent.AC20
        });
        var mechData = CreateDummyMechData(locationEquipment);

        // Act
        var mech = _mechFactory.Create(mechData);

        // Assert
        var rightTorso = mech.Parts[PartLocation.RightTorso];
        var weapon = rightTorso.GetComponents<Ac20>().ShouldHaveSingleItem();
        weapon.Name.ShouldBe("AC/20");
        weapon.Damage.ShouldBe(20);
        weapon.Heat.ShouldBe(7);
        weapon.MinimumRange.ShouldBe(0);
        weapon.ShortRange.ShouldBe(3);
        weapon.MediumRange.ShouldBe(6);
        weapon.LongRange.ShouldBe(9);
        weapon.Size.ShouldBe(10);
    }

    [Fact]
    public void Create_LRM15_ReturnsCorrectWeapon()
    {
        // Arrange
        var locationEquipment = Tuple.Create(PartLocation.LeftTorso, new List<MakaMekComponent>
        {
            MakaMekComponent.LRM15,
            MakaMekComponent.LRM15,
            MakaMekComponent.LRM15
        });
        var mechData = CreateDummyMechData(locationEquipment);

        // Act
        var mech = _mechFactory.Create(mechData);

        // Assert
        var leftTorso = mech.Parts[PartLocation.LeftTorso];
        var weapon = leftTorso.GetComponents<Lrm15>().ShouldHaveSingleItem();
        weapon.Name.ShouldBe("LRM-15");
        weapon.Damage.ShouldBe(15); // 1 damage per missile * 15 missiles
        weapon.Heat.ShouldBe(5);
        weapon.MinimumRange.ShouldBe(6);
        weapon.ShortRange.ShouldBe(7);
        weapon.MediumRange.ShouldBe(14);
        weapon.LongRange.ShouldBe(21);
        weapon.Size.ShouldBe(3);
        weapon.Clusters.ShouldBe(3); // 3 clusters of 5 missiles each
    }

    [Fact]
    public void Create_WithLRM20_ReturnsCorrectWeapon()
    {
        // Arrange
        var locationEquipment = Tuple.Create(PartLocation.LeftTorso, new List<MakaMekComponent>
        {
            MakaMekComponent.LRM20,
            MakaMekComponent.LRM20,
            MakaMekComponent.LRM20,
            MakaMekComponent.LRM20,
            MakaMekComponent.LRM20
        });
        var mechData = CreateDummyMechData(locationEquipment);

        // Act
        var mech = _mechFactory.Create(mechData);

        // Assert
        var leftTorso = mech.Parts[PartLocation.LeftTorso];
        var weapon = leftTorso.GetComponents<Lrm20>().ShouldHaveSingleItem();
        weapon.Name.ShouldBe("LRM-20");
        weapon.Damage.ShouldBe(20); // 1 damage per missile * 20 missiles
        weapon.Heat.ShouldBe(6);
        weapon.MinimumRange.ShouldBe(6);
        weapon.ShortRange.ShouldBe(7);
        weapon.MediumRange.ShouldBe(14);
        weapon.LongRange.ShouldBe(21);
        weapon.Size.ShouldBe(5);
        weapon.Clusters.ShouldBe(4); // 4 clusters of 5 missiles each
    }

    [Fact]
    public void Create_WithSRM4_ReturnsCorrectWeapon()
    {
        // Arrange
        var locationEquipment = Tuple.Create(PartLocation.RightArm, new List<MakaMekComponent> { MakaMekComponent.SRM4 });
        var mechData = CreateDummyMechData(locationEquipment);

        // Act
        var mech = _mechFactory.Create(mechData);

        // Assert
        var rightArm = mech.Parts[PartLocation.RightArm];
        var weapon = rightArm.GetComponents<Srm4>().ShouldHaveSingleItem();
        weapon.Name.ShouldBe("SRM-4");
        weapon.Damage.ShouldBe(8); // 2 damage per missile * 4 missiles
        weapon.Heat.ShouldBe(3);
        weapon.ShortRange.ShouldBe(3);
        weapon.MediumRange.ShouldBe(6);
        weapon.LongRange.ShouldBe(9);
        weapon.Clusters.ShouldBe(4);
    }

    [Fact]
    public void Create_WithSRM6_ReturnsCorrectWeapon()
    {
        // Arrange
        var locationEquipment = Tuple.Create(PartLocation.RightTorso, new List<MakaMekComponent>
        {
            MakaMekComponent.SRM6,
            MakaMekComponent.SRM6
        });
        var mechData = CreateDummyMechData(locationEquipment);

        // Act
        var mech = _mechFactory.Create(mechData);

        // Assert
        var rightTorso = mech.Parts[PartLocation.RightTorso];
        var weapon = rightTorso.GetComponents<Srm6>().ShouldHaveSingleItem();
        weapon.Name.ShouldBe("SRM-6");
        weapon.Damage.ShouldBe(12); // 2 damage per missile * 6 missiles
        weapon.Heat.ShouldBe(4);
        weapon.ShortRange.ShouldBe(3);
        weapon.MediumRange.ShouldBe(6);
        weapon.LongRange.ShouldBe(9);
        weapon.Size.ShouldBe(2);
        weapon.Clusters.ShouldBe(6);
    }

    [Fact]
    public void Create_WithHatchet_ReturnsCorrectWeapon()
    {
        // Arrange
        var locationEquipment = Tuple.Create(PartLocation.RightArm, new List<MakaMekComponent> { MakaMekComponent.Hatchet });
        var mechData = CreateDummyMechData(locationEquipment);

        // Act
        var mech = _mechFactory.Create(mechData);

        // Assert
        var rightArm = mech.Parts[PartLocation.RightArm];
        var weapon = rightArm.GetComponents<Hatchet>().ShouldHaveSingleItem();
        weapon.Name.ShouldBe("Hatchet");
        weapon.Damage.ShouldBe(0); // Base damage, actual damage is tonnage-based
        weapon.Heat.ShouldBe(0);
        weapon.Type.ShouldBe(WeaponType.Melee);
    }
    
    [Fact]
    public void Create_WithISAmmoAC2_ReturnsCorrectAmmo()
    {
        // Arrange
        var locationEquipment = Tuple.Create(PartLocation.LeftTorso, new List<MakaMekComponent> { MakaMekComponent.ISAmmoAC2 });
        var mechData = CreateDummyMechData(locationEquipment);

        // Act
        var mech = _mechFactory.Create(mechData);

        // Assert
        var leftTorso = mech.Parts[PartLocation.LeftTorso];
        var ammo = leftTorso.GetComponents<Ammo>().ShouldHaveSingleItem();
        ammo.Name.ShouldBe("AC/2 Ammo");
        ammo.ComponentType.ShouldBe(MakaMekComponent.ISAmmoAC2);
        ammo.RemainingShots.ShouldBe(45);
    }

    [Fact]
    public void Create_WithISAmmoAC10_ReturnsCorrectAmmo()
    {
        // Arrange
        var locationEquipment = Tuple.Create(PartLocation.LeftTorso, new List<MakaMekComponent> { MakaMekComponent.ISAmmoAC10 });
        var mechData = CreateDummyMechData(locationEquipment);

        // Act
        var mech = _mechFactory.Create(mechData);

        // Assert
        var leftTorso = mech.Parts[PartLocation.LeftTorso];
        var ammo = leftTorso.GetComponents<Ammo>().ShouldHaveSingleItem();
        ammo.Name.ShouldBe("AC/10 Ammo");
        ammo.ComponentType.ShouldBe(MakaMekComponent.ISAmmoAC10);
        ammo.RemainingShots.ShouldBe(10);
    }

    [Fact]
    public void Create_WithISAmmoAC20_ReturnsCorrectAmmo()
    {
        // Arrange
        var locationEquipment = Tuple.Create(PartLocation.LeftTorso, new List<MakaMekComponent> { MakaMekComponent.ISAmmoAC20 });
        var mechData = CreateDummyMechData(locationEquipment);

        // Act
        var mech = _mechFactory.Create(mechData);

        // Assert
        var leftTorso = mech.Parts[PartLocation.LeftTorso];
        var ammo = leftTorso.GetComponents<Ammo>().ShouldHaveSingleItem();
        ammo.Name.ShouldBe("AC/20 Ammo");
        ammo.ComponentType.ShouldBe(MakaMekComponent.ISAmmoAC20);
        ammo.RemainingShots.ShouldBe(5);
    }

    [Fact]
    public void Create_WithISAmmoLRM15_ReturnsCorrectAmmo()
    {
        // Arrange
        var locationEquipment = Tuple.Create(PartLocation.RightTorso, new List<MakaMekComponent> { MakaMekComponent.ISAmmoLRM15 });
        var mechData = CreateDummyMechData(locationEquipment);

        // Act
        var mech = _mechFactory.Create(mechData);

        // Assert
        var rightTorso = mech.Parts[PartLocation.RightTorso];
        var ammo = rightTorso.GetComponents<Ammo>().ShouldHaveSingleItem();
        ammo.Name.ShouldBe("LRM-15 Ammo");
        ammo.ComponentType.ShouldBe(MakaMekComponent.ISAmmoLRM15);
        ammo.RemainingShots.ShouldBe(8);
    }

    [Fact]
    public void Create_WithISAmmoLRM20_ReturnsCorrectAmmo()
    {
        // Arrange
        var locationEquipment = Tuple.Create(PartLocation.RightTorso, new List<MakaMekComponent> { MakaMekComponent.ISAmmoLRM20 });
        var mechData = CreateDummyMechData(locationEquipment);

        // Act
        var mech = _mechFactory.Create(mechData);

        // Assert
        var rightTorso = mech.Parts[PartLocation.RightTorso];
        var ammo = rightTorso.GetComponents<Ammo>().ShouldHaveSingleItem();
        ammo.Name.ShouldBe("LRM-20 Ammo");
        ammo.ComponentType.ShouldBe(MakaMekComponent.ISAmmoLRM20);
        ammo.RemainingShots.ShouldBe(6);
    }

    [Fact]
    public void Create_WithISAmmoSRM4_ReturnsCorrectAmmo()
    {
        // Arrange
        var locationEquipment = Tuple.Create(PartLocation.LeftLeg, new List<MakaMekComponent> { MakaMekComponent.ISAmmoSRM4 });
        var mechData = CreateDummyMechData(locationEquipment);

        // Act
        var mech = _mechFactory.Create(mechData);

        // Assert
        var leftLeg = mech.Parts[PartLocation.LeftLeg];
        var ammo = leftLeg.GetComponents<Ammo>().ShouldHaveSingleItem();
        ammo.Name.ShouldBe("SRM-4 Ammo");
        ammo.ComponentType.ShouldBe(MakaMekComponent.ISAmmoSRM4);
        ammo.RemainingShots.ShouldBe(25);
    }

    [Fact]
    public void Create_WithISAmmoSRM6_ReturnsCorrectAmmo()
    {
        // Arrange
        var locationEquipment = Tuple.Create(PartLocation.LeftLeg, new List<MakaMekComponent> { MakaMekComponent.ISAmmoSRM6 });
        var mechData = CreateDummyMechData(locationEquipment);

        // Act
        var mech = _mechFactory.Create(mechData);

        // Assert
        var leftLeg = mech.Parts[PartLocation.LeftLeg];
        var ammo = leftLeg.GetComponents<Ammo>().ShouldHaveSingleItem();
        ammo.Name.ShouldBe("SRM-6 Ammo");
        ammo.ComponentType.ShouldBe(MakaMekComponent.ISAmmoSRM6);
        ammo.RemainingShots.ShouldBe(15);
    }

    [Fact]
    public void CreateComponent_AllNewLevel1Equipment_DoesNotThrowExceptions()
    {
        // Arrange
        var newComponents = new[]
        {
            MakaMekComponent.SmallLaser,
            MakaMekComponent.MediumLaser,
            MakaMekComponent.LargeLaser,
            MakaMekComponent.PPC,
            MakaMekComponent.Flamer,
            MakaMekComponent.AC2,
            MakaMekComponent.AC5,
            MakaMekComponent.AC10,
            MakaMekComponent.AC20,
            MakaMekComponent.LRM5,
            MakaMekComponent.LRM10,
            MakaMekComponent.LRM15,
            MakaMekComponent.LRM20,
            MakaMekComponent.SRM2,
            MakaMekComponent.SRM4,
            MakaMekComponent.SRM6,
            MakaMekComponent.Hatchet,
            MakaMekComponent.ISAmmoAC2,
            MakaMekComponent.ISAmmoAC10,
            MakaMekComponent.ISAmmoAC20,
            MakaMekComponent.ISAmmoLRM15,
            MakaMekComponent.ISAmmoLRM20,
            MakaMekComponent.ISAmmoSRM4,
            MakaMekComponent.ISAmmoSRM6
        };

        // Act & Assert
        foreach (var component in newComponents)
        {
            var locationEquipment = Tuple.Create(PartLocation.CenterTorso, new List<MakaMekComponent> { component });
            var mechData = CreateDummyMechData(locationEquipment);

            // Should not throw any exceptions
            Should.NotThrow(() => _mechFactory.Create(mechData));
        }
    }
}
