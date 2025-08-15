using NSubstitute;
using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Ballistic;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Utils;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Community;

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
    public void CreateFromMtfData_CreatesCorrectMech()
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
    public void CreateFromMtfData_HasCorrectArmor()
    {
        // Act
        var mech = _mechFactory.Create(_unitData);

        // Assert
        mech.Parts.First(p => p.Location == PartLocation.LeftArm).CurrentArmor.ShouldBe(4);
        mech.Parts.First(p => p.Location == PartLocation.RightArm).CurrentArmor.ShouldBe(4);
        mech.Parts.First(p => p.Location == PartLocation.LeftTorso).CurrentArmor.ShouldBe(8);
        mech.Parts.First(p => p.Location == PartLocation.RightTorso).CurrentArmor.ShouldBe(8);
        mech.Parts.First(p => p.Location == PartLocation.CenterTorso).CurrentArmor.ShouldBe(10);
        mech.Parts.First(p => p.Location == PartLocation.Head).CurrentArmor.ShouldBe(8);
        mech.Parts.First(p => p.Location == PartLocation.LeftLeg).CurrentArmor.ShouldBe(8);
        mech.Parts.First(p => p.Location == PartLocation.RightLeg).CurrentArmor.ShouldBe(8);
    }

    [Fact]
    public void CreateFromMtfData_HasCorrectWeapons()
    {
        // Act
        var mech = _mechFactory.Create(_unitData);

        // Assert
        // Left Arm
        var leftArm = mech.Parts.First(p => p.Location == PartLocation.LeftArm);
        leftArm.GetComponents<Weapon>().ShouldContain(w => w.Name == "Machine Gun");
        
        // Right Arm
        var rightArm = mech.Parts.First(p => p.Location == PartLocation.RightArm);
        rightArm.GetComponents<Weapon>().ShouldContain(w => w.Name == "Medium Laser");
        
        // Center Torso
        var centerTorso = mech.Parts.First(p => p.Location == PartLocation.CenterTorso);
        var engines = centerTorso.GetComponents<Engine>().ToList();
        engines.ShouldHaveSingleItem();
        engines[0].Rating.ShouldBe(275);
        engines[0].Type.ShouldBe(EngineType.Fusion);

    }

    [Fact]
    public void CreateFromMtfData_HasCorrectActuators()
    {

        // Act
        var mech = _mechFactory.Create(_unitData);

        // Assert
        var leftArm = mech.Parts.First(p => p.Location == PartLocation.LeftArm);
        leftArm.GetComponents<Component>().ShouldContain(a => a.Name == "Shoulder");

        var rightArm = mech.Parts.First(p => p.Location == PartLocation.RightArm);
        rightArm.GetComponents<Component>().ShouldContain(a => a.Name == "Shoulder");
        rightArm.GetComponents<Component>().ShouldContain(a => a.Name == "Upper Arm Actuator");
    }
    
    [Fact]
    public void CreateFromMtfData_CorrectlyAddsOneComponentThatOccupiesSeveralSlots()
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
        var leftTorso = mech.Parts.First(p => p.Location == PartLocation.LeftTorso);
        var weapon = leftTorso.GetComponents<Ac5>();
        weapon.Count().ShouldBe(1); 
    }
    [Fact]
    public void CreateFromMtfData_CorrectlyAddsTwoComponentsThatOccupySeveralSlots()
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
        var leftTorso = mech.Parts.First(p => p.Location == PartLocation.LeftTorso);
        var weapon = leftTorso.GetComponents<Ac5>();
        weapon.Count().ShouldBe(2); 
    }
}
