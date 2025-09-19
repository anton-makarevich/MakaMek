using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Ballistic;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Melee;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;

namespace Sanet.MakaMek.Core.Data.Units;

/// <summary>
/// Interface for component definition registry
/// </summary>
public interface IComponentDefinitionRegistry
{
    ComponentDefinition GetDefinition(MakaMekComponent componentType);
    Component CreateComponent(MakaMekComponent componentType, ComponentData? componentData = null);
}

/// <summary>
/// Registry containing all component definitions and factory methods
/// </summary>
public class ComponentDefinitionRegistry : IComponentDefinitionRegistry
{
    private readonly Dictionary<MakaMekComponent, ComponentDefinition> _definitions;
    private readonly Dictionary<MakaMekComponent, Func<ComponentData?, Component>> _factories;

    public ComponentDefinitionRegistry()
    {
        _definitions = InitializeDefinitions();
        _factories = InitializeFactories();
    }

    public ComponentDefinition GetDefinition(MakaMekComponent componentType)
    {
        if (_definitions.TryGetValue(componentType, out var definition))
            return definition;

        throw new ArgumentException($"No definition found for component type: {componentType}");
    }

    public Component CreateComponent(MakaMekComponent componentType, ComponentData? componentData = null)
    {
        if (_factories.TryGetValue(componentType, out var factory))
            return factory(componentData);

        throw new ArgumentException($"No factory found for component type: {componentType}");
    }

    private Dictionary<MakaMekComponent, ComponentDefinition> InitializeDefinitions()
    {
        return new Dictionary<MakaMekComponent, ComponentDefinition>
        {
            // Actuators
            [MakaMekComponent.Shoulder] = new ActuatorDefinition("Shoulder", MakaMekComponent.Shoulder),
            [MakaMekComponent.UpperArmActuator] = new ActuatorDefinition("Upper Arm Actuator", MakaMekComponent.UpperArmActuator),
            [MakaMekComponent.LowerArmActuator] = new ActuatorDefinition("Lower Arm", MakaMekComponent.LowerArmActuator),
            [MakaMekComponent.HandActuator] = new ActuatorDefinition("Hand Actuator", MakaMekComponent.HandActuator),
            [MakaMekComponent.Hip] = new ActuatorDefinition("Hip", MakaMekComponent.Hip),
            [MakaMekComponent.UpperLegActuator] = new ActuatorDefinition("Upper Leg", MakaMekComponent.UpperLegActuator),
            [MakaMekComponent.LowerLegActuator] = new ActuatorDefinition("Lower Leg", MakaMekComponent.LowerLegActuator),
            [MakaMekComponent.FootActuator] = new ActuatorDefinition("Foot Actuator", MakaMekComponent.FootActuator),

            // Internal Components
            [MakaMekComponent.Gyro] = new InternalDefinition("Gyro", 2, MakaMekComponent.Gyro, 4),
            [MakaMekComponent.LifeSupport] = new InternalDefinition("Life Support", 1, MakaMekComponent.LifeSupport),
            [MakaMekComponent.Sensors] = new InternalDefinition("Sensors", 2, MakaMekComponent.Sensors),
            [MakaMekComponent.Cockpit] = new InternalDefinition("Cockpit", 1, MakaMekComponent.Cockpit),

            // Equipment
            [MakaMekComponent.HeatSink] = new EquipmentDefinition("Heat Sink", MakaMekComponent.HeatSink, 0),
            [MakaMekComponent.JumpJet] = new EquipmentDefinition("Jump Jets", MakaMekComponent.JumpJet, 0),

            // Weapons - using existing static definitions
            [MakaMekComponent.MachineGun] = MachineGun.Definition,
            [MakaMekComponent.SmallLaser] = SmallLaser.Definition,
            [MakaMekComponent.MediumLaser] = MediumLaser.Definition,
            [MakaMekComponent.LargeLaser] = LargeLaser.Definition,
            [MakaMekComponent.PPC] = Ppc.Definition,
            [MakaMekComponent.Flamer] = Flamer.Definition,
            [MakaMekComponent.AC2] = Ac2.Definition,
            [MakaMekComponent.AC5] = Ac5.Definition,
            [MakaMekComponent.AC10] = Ac10.Definition,
            [MakaMekComponent.AC20] = Ac20.Definition,
            [MakaMekComponent.LRM5] = Lrm5.Definition,
            [MakaMekComponent.LRM10] = Lrm10.Definition,
            [MakaMekComponent.LRM15] = Lrm15.Definition,
            [MakaMekComponent.LRM20] = Lrm20.Definition,
            [MakaMekComponent.SRM2] = Srm2.Definition,
            [MakaMekComponent.SRM4] = Srm4.Definition,
            [MakaMekComponent.SRM6] = Srm6.Definition,
            [MakaMekComponent.Hatchet] = Hatchet.Definition,
        };
    }

    private Dictionary<MakaMekComponent, Func<ComponentData?, Component>> InitializeFactories()
    {
        return new Dictionary<MakaMekComponent, Func<ComponentData?, Component>>
        {
            // Actuators
            [MakaMekComponent.Shoulder] = data => new ShoulderActuator(),
            [MakaMekComponent.UpperArmActuator] = data => new UpperArmActuator(),
            [MakaMekComponent.LowerArmActuator] = data => new LowerArmActuator(),
            [MakaMekComponent.HandActuator] = data => new HandActuator(),
            [MakaMekComponent.Hip] = data => new HipActuator(),
            [MakaMekComponent.UpperLegActuator] = data => new UpperLegActuator(),
            [MakaMekComponent.LowerLegActuator] = data => new LowerLegActuator(),
            [MakaMekComponent.FootActuator] = data => new FootActuator(),

            // Internal Components
            [MakaMekComponent.Gyro] = data => new Gyro(),
            [MakaMekComponent.LifeSupport] = data => new LifeSupport(),
            [MakaMekComponent.Sensors] = data => new Sensors(),
            [MakaMekComponent.Cockpit] = data => new Cockpit(),

            // Equipment
            [MakaMekComponent.HeatSink] = data => new HeatSink(),
            [MakaMekComponent.JumpJet] = data => CreateJumpJet(data),

            // Engine (special case with state data)
            [MakaMekComponent.Engine] = data => CreateEngine(data),

            // Weapons
            [MakaMekComponent.MachineGun] = data => new MachineGun(),
            [MakaMekComponent.SmallLaser] = data => new SmallLaser(),
            [MakaMekComponent.MediumLaser] = data => new MediumLaser(),
            [MakaMekComponent.LargeLaser] = data => new LargeLaser(),
            [MakaMekComponent.PPC] = data => new Ppc(),
            [MakaMekComponent.Flamer] = data => new Flamer(),
            [MakaMekComponent.AC2] = data => new Ac2(),
            [MakaMekComponent.AC5] = data => new Ac5(),
            [MakaMekComponent.AC10] = data => new Ac10(),
            [MakaMekComponent.AC20] = data => new Ac20(),
            [MakaMekComponent.LRM5] = data => new Lrm5(),
            [MakaMekComponent.LRM10] = data => new Lrm10(),
            [MakaMekComponent.LRM15] = data => new Lrm15(),
            [MakaMekComponent.LRM20] = data => new Lrm20(),
            [MakaMekComponent.SRM2] = data => new Srm2(),
            [MakaMekComponent.SRM4] = data => new Srm4(),
            [MakaMekComponent.SRM6] = data => new Srm6(),
            [MakaMekComponent.Hatchet] = data => new Hatchet(),

            // Ammo
            [MakaMekComponent.ISAmmoMG] = data => CreateAmmo(MachineGun.Definition, data),
            [MakaMekComponent.ISAmmoAC2] = data => CreateAmmo(Ac2.Definition, data),
            [MakaMekComponent.ISAmmoAC5] = data => CreateAmmo(Ac5.Definition, data),
            [MakaMekComponent.ISAmmoAC10] = data => CreateAmmo(Ac10.Definition, data),
            [MakaMekComponent.ISAmmoAC20] = data => CreateAmmo(Ac20.Definition, data),
            [MakaMekComponent.ISAmmoLRM5] = data => CreateAmmo(Lrm5.Definition, data),
            [MakaMekComponent.ISAmmoLRM10] = data => CreateAmmo(Lrm10.Definition, data),
            [MakaMekComponent.ISAmmoLRM15] = data => CreateAmmo(Lrm15.Definition, data),
            [MakaMekComponent.ISAmmoLRM20] = data => CreateAmmo(Lrm20.Definition, data),
            [MakaMekComponent.ISAmmoSRM2] = data => CreateAmmo(Srm2.Definition, data),
            [MakaMekComponent.ISAmmoSRM4] = data => CreateAmmo(Srm4.Definition, data),
            [MakaMekComponent.ISAmmoSRM6] = data => CreateAmmo(Srm6.Definition, data),
        };
    }

    private static Component CreateEngine(ComponentData? data)
    {
        if (data?.SpecificData is EngineStateData engineState)
        {
            return new Engine(engineState.Rating, engineState.Type);
        }

        // Default engine if no state data provided
        return new Engine(200, EngineType.Fusion);
    }

    private static Component CreateJumpJet(ComponentData? data)
    {
        if (data?.SpecificData is JumpJetStateData jumpJetState)
        {
            return new JumpJets(jumpJetState.JumpMp);
        }

        // Default jump jet if no state data provided
        return new JumpJets(1);
    }

    private static Component CreateAmmo(WeaponDefinition weaponDefinition, ComponentData? data)
    {
        var remainingShots = weaponDefinition.FullAmmoRounds;

        if (data?.SpecificData is AmmoStateData ammoState)
        {
            remainingShots = ammoState.RemainingShots;
        }

        return new Ammo(weaponDefinition, remainingShots);
    }
}