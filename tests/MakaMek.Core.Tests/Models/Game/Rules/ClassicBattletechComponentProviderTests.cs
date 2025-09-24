using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Rules;

    public class ClassicBattletechComponentProviderTests
    {
        private readonly ClassicBattletechComponentProvider _sut = new();
        
        [Theory]
        [InlineData(MakaMekComponent.Shoulder)]
        [InlineData(MakaMekComponent.Hip)]
        [InlineData(MakaMekComponent.UpperLegActuator)]
        [InlineData(MakaMekComponent.LowerLegActuator)]
        [InlineData(MakaMekComponent.FootActuator)]
        public void GetDefinition_ValidFixedActuatorComponent_ShouldReturnCorrectDefinition(MakaMekComponent componentType)
        {
            // Act
            var result = _sut.GetDefinition(componentType);

            // Assert
            result.ShouldNotBeNull();
            result.ComponentType.ShouldBe(componentType);
            result.Name.ShouldNotBeNullOrEmpty();
            result.Size.ShouldBe(1);
            result.HealthPoints.ShouldBe(1);
            result.BattleValue.ShouldBe(0);
            result.IsRemovable.ShouldBeFalse();
        }
        
        [Theory]
        [InlineData(MakaMekComponent.UpperArmActuator)]
        [InlineData(MakaMekComponent.LowerArmActuator)]
        [InlineData(MakaMekComponent.HandActuator)]
        public void GetDefinition_ValidNonFixedActuatorComponent_ShouldReturnCorrectDefinition(MakaMekComponent componentType)
        {
            // Act
            var result = _sut.GetDefinition(componentType);

            // Assert
            result.ShouldNotBeNull();
            result.ComponentType.ShouldBe(componentType);
            result.Name.ShouldNotBeNullOrEmpty();
            result.Size.ShouldBe(1);
            result.HealthPoints.ShouldBe(1);
            result.BattleValue.ShouldBe(0);
            result.IsRemovable.ShouldBeTrue();
        }

        [Theory]
        [InlineData(MakaMekComponent.Gyro)]
        [InlineData(MakaMekComponent.LifeSupport)]
        [InlineData(MakaMekComponent.Sensors)]
        [InlineData(MakaMekComponent.Cockpit)]
        public void GetDefinition_ValidInternalComponent_ShouldReturnCorrectDefinition(MakaMekComponent componentType)
        {
            // Arrange
            var expectedSize = componentType switch
            {
                MakaMekComponent.Gyro => 4,
                MakaMekComponent.Cockpit => 1,
                _ => 2
            };
            var expectedHealthPoints = componentType switch
            {
                MakaMekComponent.Gyro => 2,
                MakaMekComponent.Sensors => 2,
                _ => 1
            };
            
            // Act
            var result = _sut.GetDefinition(componentType);

            // Assert
            result.ShouldNotBeNull();
            result.ComponentType.ShouldBe(componentType);
            result.Name.ShouldNotBeNullOrEmpty();
            result.Size.ShouldBe(expectedSize);
            result.HealthPoints.ShouldBe(expectedHealthPoints);
            result.BattleValue.ShouldBe(0);
            result.IsRemovable.ShouldBeFalse();
        }

        [Theory]
        [InlineData(MakaMekComponent.HeatSink)]
        [InlineData(MakaMekComponent.JumpJet)]
        public void GetDefinition_ValidEquipmentComponent_ShouldReturnCorrectDefinition(MakaMekComponent componentType)
        {
            // Act
            var result = _sut.GetDefinition(componentType);

            // Assert
            result.ShouldNotBeNull();
            result.ComponentType.ShouldBe(componentType);
            result.Name.ShouldNotBeNullOrEmpty();
            result.Size.ShouldBe(1);
            result.HealthPoints.ShouldBe(1);
            result.BattleValue.ShouldBe(0);
            result.IsRemovable.ShouldBeTrue();
        }

        [Theory]
        [InlineData(MakaMekComponent.MachineGun)]
        [InlineData(MakaMekComponent.SmallLaser)]
        [InlineData(MakaMekComponent.MediumLaser)]
        [InlineData(MakaMekComponent.LargeLaser)]
        [InlineData(MakaMekComponent.PPC)]
        [InlineData(MakaMekComponent.Flamer)]
        [InlineData(MakaMekComponent.AC2)]
        [InlineData(MakaMekComponent.AC5)]
        [InlineData(MakaMekComponent.AC10)]
        [InlineData(MakaMekComponent.AC20)]
        [InlineData(MakaMekComponent.LRM5)]
        [InlineData(MakaMekComponent.LRM10)]
        [InlineData(MakaMekComponent.LRM15)]
        [InlineData(MakaMekComponent.LRM20)]
        [InlineData(MakaMekComponent.SRM2)]
        [InlineData(MakaMekComponent.SRM4)]
        [InlineData(MakaMekComponent.SRM6)]
        [InlineData(MakaMekComponent.Hatchet)]
        public void GetDefinition_ValidWeaponComponent_ShouldReturnCorrectDefinition(MakaMekComponent componentType)
        {
            // Act
            var result = _sut.GetDefinition(componentType);

            // Assert
            result.ShouldNotBeNull();
            result.ComponentType.ShouldBe(componentType);
            result.Name.ShouldNotBeNullOrEmpty();
            result.Size.ShouldBeGreaterThan(0);
            result.HealthPoints.ShouldBeGreaterThan(0);
            result.BattleValue.ShouldBeGreaterThanOrEqualTo(0);
        }

        [Theory]
        [InlineData(MakaMekComponent.ISAmmoMG)]
        [InlineData(MakaMekComponent.ISAmmoSRM2)]
        [InlineData(MakaMekComponent.ISAmmoSRM4)]
        [InlineData(MakaMekComponent.ISAmmoSRM6)]
        [InlineData(MakaMekComponent.ISAmmoLRM5)]
        [InlineData(MakaMekComponent.ISAmmoLRM10)]
        [InlineData(MakaMekComponent.ISAmmoLRM15)]
        [InlineData(MakaMekComponent.ISAmmoLRM20)]
        [InlineData(MakaMekComponent.ISAmmoAC2)]
        [InlineData(MakaMekComponent.ISAmmoAC5)]
        [InlineData(MakaMekComponent.ISAmmoAC10)]
        [InlineData(MakaMekComponent.ISAmmoAC20)]
        public void GetDefinition_ValidAmmoComponent_ShouldReturnCorrectDefinition(MakaMekComponent componentType)
        {
            // Act
            var result = _sut.GetDefinition(componentType);

            // Assert
            result.ShouldNotBeNull();
            result.ComponentType.ShouldBe(componentType);
            result.Name.ShouldNotBeNullOrEmpty();
            result.Size.ShouldBe(1);
            result.HealthPoints.ShouldBe(1);
            result.BattleValue.ShouldBe(0);
        }

        [Fact]
        public void GetDefinition_EngineComponent_ShouldReturnCorrectDefinition()
        {
            // Arrange
            const MakaMekComponent componentType = MakaMekComponent.Engine;
            var specificData = new EngineStateData(EngineType.Fusion, 200);
            
            // Act
            var result = _sut.GetDefinition(componentType, specificData) as EngineDefinition;

            // Assert
            result.ShouldNotBeNull();
            result.ComponentType.ShouldBe(componentType);
            result.Name.ShouldNotBeNullOrEmpty();
            result.Size.ShouldBe(6);
            result.HealthPoints.ShouldBe(3);
            result.Rating.ShouldBe(200);
            result.Type.ShouldBe(EngineType.Fusion);
        }

        [Fact]
        public void GetDefinition_EngineComponentWithoutSpecificData_ShouldReturnNull()
        {
            // Arrange
            const MakaMekComponent componentType = MakaMekComponent.Engine;
            
            // Act
            var result = _sut.GetDefinition(componentType);

            // Assert
            result.ShouldBeNull();
        }

        [Fact]
        public void GetDefinition_InvalidComponentType_ShouldReturnNull()
        {
            // Arrange
            var invalidComponentType = (MakaMekComponent)999;

            // Act
            var result = _sut.GetDefinition(invalidComponentType);

            // Assert
            result.ShouldBeNull();
        }
        
        [Theory]
        [InlineData(MakaMekComponent.Shoulder)]
        [InlineData(MakaMekComponent.UpperArmActuator)]
        [InlineData(MakaMekComponent.LowerArmActuator)]
        [InlineData(MakaMekComponent.HandActuator)]
        [InlineData(MakaMekComponent.Hip)]
        [InlineData(MakaMekComponent.UpperLegActuator)]
        [InlineData(MakaMekComponent.LowerLegActuator)]
        [InlineData(MakaMekComponent.FootActuator)]
        public void CreateComponent_ValidActuatorComponent_ShouldReturnCorrectComponent(MakaMekComponent componentType)
        {
            // Act
            var result = _sut.CreateComponent(componentType);

            // Assert
            result.ShouldNotBeNull();
            result.ComponentType.ShouldBe(componentType);
            result.Name.ShouldNotBeNullOrEmpty();
            result.Size.ShouldBe(1);
            result.HealthPoints.ShouldBe(1);
            result.IsActive.ShouldBe(true);
            result.Hits.ShouldBe(0);
        }

        [Theory]
        [InlineData(MakaMekComponent.Gyro)]
        [InlineData(MakaMekComponent.LifeSupport)]
        [InlineData(MakaMekComponent.Sensors)]
        [InlineData(MakaMekComponent.Cockpit)]
        public void CreateComponent_ValidInternalComponent_ShouldReturnCorrectComponent(MakaMekComponent componentType)
        {
            // Arrange
            var expectedSize = componentType switch
            {
                MakaMekComponent.Gyro => 4,
                MakaMekComponent.Cockpit => 1,
                _ => 2
            };
            var expectedHealthPoints = componentType switch
            {
                MakaMekComponent.Gyro => 2,
                MakaMekComponent.Sensors => 2,
                _ => 1
            };
            
            // Act
            var result = _sut.CreateComponent(componentType);

            // Assert
            result.ShouldNotBeNull();
            result.ComponentType.ShouldBe(componentType);
            result.Name.ShouldNotBeNullOrEmpty();
            result.Size.ShouldBe(expectedSize);
            result.HealthPoints.ShouldBe(expectedHealthPoints);
            result.IsActive.ShouldBe(true);
            result.Hits.ShouldBe(0);
        }

        [Theory]
        [InlineData(MakaMekComponent.HeatSink)]
        [InlineData(MakaMekComponent.JumpJet)]
        public void CreateComponent_ValidEquipmentComponent_ShouldReturnCorrectComponent(MakaMekComponent componentType)
        {
            // Act
            var result = _sut.CreateComponent(componentType);

            // Assert
            result.ShouldNotBeNull();
            result.ComponentType.ShouldBe(componentType);
            result.Name.ShouldNotBeNullOrEmpty();
            result.Size.ShouldBe(1);
            result.HealthPoints.ShouldBe(1);
            result.IsActive.ShouldBe(true);
            result.Hits.ShouldBe(0);
        }

        [Theory]
        [InlineData(MakaMekComponent.MachineGun)]
        [InlineData(MakaMekComponent.SmallLaser)]
        [InlineData(MakaMekComponent.MediumLaser)]
        [InlineData(MakaMekComponent.LargeLaser)]
        [InlineData(MakaMekComponent.PPC)]
        [InlineData(MakaMekComponent.Flamer)]
        [InlineData(MakaMekComponent.AC2)]
        [InlineData(MakaMekComponent.AC5)]
        [InlineData(MakaMekComponent.AC10)]
        [InlineData(MakaMekComponent.AC20)]
        [InlineData(MakaMekComponent.LRM5)]
        [InlineData(MakaMekComponent.LRM10)]
        [InlineData(MakaMekComponent.LRM15)]
        [InlineData(MakaMekComponent.LRM20)]
        [InlineData(MakaMekComponent.SRM2)]
        [InlineData(MakaMekComponent.SRM4)]
        [InlineData(MakaMekComponent.SRM6)]
        [InlineData(MakaMekComponent.Hatchet)]
        public void CreateComponent_ValidWeaponComponent_ShouldReturnCorrectComponent(MakaMekComponent componentType)
        {
            // Act
            var result = _sut.CreateComponent(componentType);

            // Assert
            result.ShouldNotBeNull();
            result.ShouldBeAssignableTo<Weapon>();
            result.ComponentType.ShouldBe(componentType);
            result.Name.ShouldNotBeNullOrEmpty();
            result.Size.ShouldBeGreaterThan(0);
            result.HealthPoints.ShouldBeGreaterThan(0);
            result.IsActive.ShouldBe(true);
            result.Hits.ShouldBe(0);
        }

        [Theory]
        [InlineData(MakaMekComponent.ISAmmoMG)]
        [InlineData(MakaMekComponent.ISAmmoSRM2)]
        [InlineData(MakaMekComponent.ISAmmoSRM4)]
        [InlineData(MakaMekComponent.ISAmmoSRM6)]
        [InlineData(MakaMekComponent.ISAmmoLRM5)]
        [InlineData(MakaMekComponent.ISAmmoLRM10)]
        [InlineData(MakaMekComponent.ISAmmoLRM15)]
        [InlineData(MakaMekComponent.ISAmmoLRM20)]
        [InlineData(MakaMekComponent.ISAmmoAC2)]
        [InlineData(MakaMekComponent.ISAmmoAC5)]
        [InlineData(MakaMekComponent.ISAmmoAC10)]
        [InlineData(MakaMekComponent.ISAmmoAC20)]
        public void CreateComponent_ValidAmmoComponent_ShouldReturnCorrectComponent(MakaMekComponent componentType)
        {
            // Act
            var result = _sut.CreateComponent(componentType);

            // Assert
            result.ShouldNotBeNull();
            result.ShouldBeOfType<Ammo>();
            result.ComponentType.ShouldBe(componentType);
            result.Name.ShouldNotBeNullOrEmpty();
            result.Size.ShouldBe(1);
            result.HealthPoints.ShouldBe(1);
            result.IsActive.ShouldBe(true);
            result.Hits.ShouldBe(0);
        }

        [Fact]
        public void CreateComponent_EngineComponent_ShouldReturnEngineWithDefaultValues()
        {
            // Arrange
            var engineData = new ComponentData
            {
                Type = MakaMekComponent.Engine,
                Assignments = [],
                SpecificData = new EngineStateData(EngineType.Fusion, 200)
            };
            
            // Act
            var result = _sut.CreateComponent(MakaMekComponent.Engine, engineData);

            // Assert
            result.ShouldNotBeNull();
            result.ShouldBeOfType<Engine>();
            result.ComponentType.ShouldBe(MakaMekComponent.Engine);
            result.Name.ShouldBe("Fusion Engine 200");
            result.Size.ShouldBeGreaterThan(0);
            result.HealthPoints.ShouldBeGreaterThan(0);
            result.IsActive.ShouldBe(true);
            result.Hits.ShouldBe(0);
        }

        [Fact]
        public void CreateComponent_InvalidComponentType_ShouldReturnNull()
        {
            // Arrange
            var invalidComponentType = (MakaMekComponent)999;

            // Act
            var result = _sut.CreateComponent(invalidComponentType);

            // Assert
            result.ShouldBeNull();
        }

        [Theory]
        [InlineData(MakaMekComponent.Shoulder)]
        [InlineData(MakaMekComponent.UpperArmActuator)]
        [InlineData(MakaMekComponent.LowerArmActuator)]
        [InlineData(MakaMekComponent.HandActuator)]
        [InlineData(MakaMekComponent.Hip)]
        [InlineData(MakaMekComponent.UpperLegActuator)]
        [InlineData(MakaMekComponent.LowerLegActuator)]
        [InlineData(MakaMekComponent.FootActuator)]
        public void CreateComponent_WithComponentData_ValidActuatorComponent_ShouldReturnComponentWithCorrectState(MakaMekComponent componentType)
        {
            // Arrange
            var componentData = new ComponentData
            {
                Type = componentType,
                Assignments = [],
                Hits = 2,
                IsActive = false,
                Name = "Custom Actuator Name",
                Manufacturer = "Custom Manufacturer"
            };

            // Act
            var result = _sut.CreateComponent(componentType, componentData);

            // Assert
            result.ShouldNotBeNull();
            result.ComponentType.ShouldBe(componentType);
            result.Name.ShouldBe(componentData.Name);
            result.Manufacturer.ShouldBe(componentData.Manufacturer);
            result.Hits.ShouldBe(componentData.Hits);
            result.IsActive.ShouldBe(componentData.IsActive);
        }

        [Theory]
        [InlineData(MakaMekComponent.MachineGun)]
        [InlineData(MakaMekComponent.SmallLaser)]
        [InlineData(MakaMekComponent.MediumLaser)]
        [InlineData(MakaMekComponent.LargeLaser)]
        [InlineData(MakaMekComponent.PPC)]
        [InlineData(MakaMekComponent.Flamer)]
        public void CreateComponent_WithComponentData_ValidWeaponComponent_ShouldReturnComponentWithCorrectState(MakaMekComponent componentType)
        {
            // Arrange
            var componentData = new ComponentData
            {
                Type = componentType,
                Assignments = new List<LocationSlotAssignment>(),
                Hits = 1,
                IsActive = true,
                Name = "Custom Weapon Name",
                Manufacturer = "Custom Weapon Manufacturer"
            };

            // Act
            var result = _sut.CreateComponent(componentType, componentData);

            // Assert
            result.ShouldNotBeNull();
            result.ComponentType.ShouldBe(componentType);
            result.Name.ShouldBe(componentData.Name);
            result.Manufacturer.ShouldBe(componentData.Manufacturer);
            result.Hits.ShouldBe(componentData.Hits);
            result.IsActive.ShouldBe(componentData.IsActive);
        }

        [Theory]
        [InlineData(MakaMekComponent.ISAmmoMG)]
        [InlineData(MakaMekComponent.ISAmmoSRM2)]
        [InlineData(MakaMekComponent.ISAmmoSRM4)]
        [InlineData(MakaMekComponent.ISAmmoLRM5)]
        [InlineData(MakaMekComponent.ISAmmoAC2)]
        public void CreateComponent_WithComponentData_ValidAmmoComponent_ShouldReturnComponentWithCorrectState(MakaMekComponent componentType)
        {
            // Arrange
            var ammoStateData = new AmmoStateData(RemainingShots: 5);
            var componentData = new ComponentData
            {
                Type = componentType,
                Assignments = [],
                Hits = 0,
                IsActive = true,
                SpecificData = ammoStateData
            };

            // Act
            var result = _sut.CreateComponent(componentType, componentData);

            // Assert
            result.ShouldNotBeNull();
            result.ShouldBeOfType<Ammo>();
            result.ComponentType.ShouldBe(componentType);
            result.Hits.ShouldBe(componentData.Hits);
            result.IsActive.ShouldBe(componentData.IsActive);

            // Verify ammo-specific state is applied correctly
            var ammo = (Ammo)result;
            ammo.RemainingShots.ShouldBe(ammoStateData.RemainingShots);
        }
        
        [Fact]
        public void CreateComponent_AmmoWithFullComponentData_ShouldRestoreAllState()
        {
            // Arrange  
            var componentData = new ComponentData
            {
                Type = MakaMekComponent.ISAmmoLRM5,
                Name = "Custom Ammo Name",
                Manufacturer = "Custom Manufacturer",
                Hits = 1,
                IsActive = false,
                HasExploded = false,
                SpecificData = new AmmoStateData(RemainingShots: 10),
                Assignments = []
            };
    
            // Act
            var result = _sut.CreateComponent(MakaMekComponent.ISAmmoLRM5, componentData);
    
            // Assert
            result.ShouldNotBeNull();
            var ammo = result.ShouldBeOfType<Ammo>();
            ammo.Name.ShouldBe("Custom Ammo Name");
            ammo.Manufacturer.ShouldBe("Custom Manufacturer");
            ammo.Hits.ShouldBe(1);
            ammo.IsActive.ShouldBe(false);
            ammo.HasExploded.ShouldBe(false);
            ammo.RemainingShots.ShouldBe(10);
        }

        [Fact]
        public void CreateComponent_EngineWithoutStateData_ShouldReturnNull()
        {
            // Act
            var result = _sut.CreateComponent(MakaMekComponent.Engine);

            // Assert
            result.ShouldBeNull();
        }

        [Fact]
        public void CreateComponent_WithComponentDataWrongType_ShouldIgnoreSpecificData()
        {
            // Arrange
            var ammoStateData = new AmmoStateData(RemainingShots: 5);
            var componentData = new ComponentData
            {
                Type = MakaMekComponent.MachineGun, // MachineGun doesn't use AmmoStateData
                Assignments = new List<LocationSlotAssignment>(),
                SpecificData = ammoStateData
            };

            // Act
            var result = _sut.CreateComponent(MakaMekComponent.MachineGun, componentData);

            // Assert
            result.ShouldNotBeNull();
            result.ComponentType.ShouldBe(MakaMekComponent.MachineGun);
            result.Hits.ShouldBe(0);
            result.IsActive.ShouldBe(true);
        }
    }

