using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Ballistic;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Melee;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;

namespace Sanet.MakaMek.Core.Models.Game.Rules;

/// <summary>
/// Registry containing all component definitions and factory methods
/// </summary>
public class ClassicBattletechComponentProvider : IComponentProvider
{
    private readonly Dictionary<MakaMekComponent, ComponentDefinition> _definitions;
    private readonly Dictionary<MakaMekComponent, Func<ComponentData?, Component?>> _factories;

    public ClassicBattletechComponentProvider()
    {
        _definitions = InitializeDefinitions();
        _factories = InitializeFactories();
    }

    public ComponentDefinition? GetDefinition(MakaMekComponent componentType,ComponentSpecificData? specificData = null)
    {
        if (componentType == MakaMekComponent.Engine && specificData is EngineStateData engineState)
        {
            return Engine.CreateEngineDefinition(engineState.Type, engineState.Rating);
        }
        return _definitions.GetValueOrDefault(componentType);
    }

    public Component? CreateComponent(MakaMekComponent componentType, ComponentData? componentData = null)
    {
        return _factories.TryGetValue(componentType, out var factory) ? factory(componentData) : null;
    }

    private Dictionary<MakaMekComponent, ComponentDefinition> InitializeDefinitions()
    {
        return new Dictionary<MakaMekComponent, ComponentDefinition>
        {
            // Actuators - using static Definition properties
            [MakaMekComponent.Shoulder] = ShoulderActuator.Definition,
            [MakaMekComponent.UpperArmActuator] = UpperArmActuator.Definition,
            [MakaMekComponent.LowerArmActuator] = LowerArmActuator.Definition,
            [MakaMekComponent.HandActuator] = HandActuator.Definition,
            [MakaMekComponent.Hip] = HipActuator.Definition,
            [MakaMekComponent.UpperLegActuator] = UpperLegActuator.Definition,
            [MakaMekComponent.LowerLegActuator] = LowerLegActuator.Definition,
            [MakaMekComponent.FootActuator] = FootActuator.Definition,

            // Internal Components - using static Definition properties
            [MakaMekComponent.Gyro] = Gyro.Definition,
            [MakaMekComponent.LifeSupport] = LifeSupport.Definition,
            [MakaMekComponent.Sensors] = Sensors.Definition,
            [MakaMekComponent.Cockpit] = Cockpit.Definition,

            // Equipment - using static Definition properties
            [MakaMekComponent.HeatSink] = HeatSink.Definition,
            [MakaMekComponent.JumpJet] = JumpJets.Definition,

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
            
            // Ammo - using existing static weapon definitions
            [MakaMekComponent.ISAmmoMG] = Ammo.CreateAmmoDefinition(MachineGun.Definition),
            [MakaMekComponent.ISAmmoAC2] = Ammo.CreateAmmoDefinition(Ac2.Definition),
            [MakaMekComponent.ISAmmoAC5] = Ammo.CreateAmmoDefinition(Ac5.Definition),
            [MakaMekComponent.ISAmmoAC10] = Ammo.CreateAmmoDefinition(Ac10.Definition),
            [MakaMekComponent.ISAmmoAC20] = Ammo.CreateAmmoDefinition(Ac20.Definition),
            [MakaMekComponent.ISAmmoLRM5] = Ammo.CreateAmmoDefinition(Lrm5.Definition),
            [MakaMekComponent.ISAmmoLRM10] = Ammo.CreateAmmoDefinition(Lrm10.Definition),
            [MakaMekComponent.ISAmmoLRM15] = Ammo.CreateAmmoDefinition(Lrm15.Definition),
            [MakaMekComponent.ISAmmoLRM20] = Ammo.CreateAmmoDefinition(Lrm20.Definition),
            [MakaMekComponent.ISAmmoSRM2] = Ammo.CreateAmmoDefinition(Srm2.Definition),
            [MakaMekComponent.ISAmmoSRM4] = Ammo.CreateAmmoDefinition(Srm4.Definition),
            [MakaMekComponent.ISAmmoSRM6] = Ammo.CreateAmmoDefinition(Srm6.Definition)
        };
    }

    private Dictionary<MakaMekComponent, Func<ComponentData?, Component?>> InitializeFactories()
    {
        return new Dictionary<MakaMekComponent, Func<ComponentData?, Component?>>
        {
            // Actuators
            [MakaMekComponent.Shoulder] = data => new ShoulderActuator(data),
            [MakaMekComponent.UpperArmActuator] = data => new UpperArmActuator(data),
            [MakaMekComponent.LowerArmActuator] = data => new LowerArmActuator(data),
            [MakaMekComponent.HandActuator] = data => new HandActuator(data),
            [MakaMekComponent.Hip] = data => new HipActuator(data),
            [MakaMekComponent.UpperLegActuator] = data => new UpperLegActuator(data),
            [MakaMekComponent.LowerLegActuator] = data => new LowerLegActuator(data),
            [MakaMekComponent.FootActuator] = data => new FootActuator(data),

            // Internal Components
            [MakaMekComponent.Gyro] = data => new Gyro(data),
            [MakaMekComponent.LifeSupport] = data => new LifeSupport(data),
            [MakaMekComponent.Sensors] = data => new Sensors(data),
            [MakaMekComponent.Cockpit] = data => new Cockpit(data),

            // Equipment
            [MakaMekComponent.HeatSink] = data => new HeatSink(data),
            [MakaMekComponent.JumpJet] = data => new JumpJets(data),

            // Engine (special case with state data)
            [MakaMekComponent.Engine] = CreateEngine,

            // Weapons - Note: Most weapons don't support ComponentData yet due to primary constructors
            [MakaMekComponent.MachineGun] = data => new MachineGun(data),
            [MakaMekComponent.SmallLaser] = data => new SmallLaser(data),
            [MakaMekComponent.MediumLaser] = data => new MediumLaser(data),
            [MakaMekComponent.LargeLaser] = data => new LargeLaser(data),
            [MakaMekComponent.PPC] = data => new Ppc(data),
            [MakaMekComponent.Flamer] = data => new Flamer(data),
            [MakaMekComponent.AC2] = data => new Ac2(data),
            [MakaMekComponent.AC5] = data => new Ac5(data),
            [MakaMekComponent.AC10] = data => new Ac10(data),
            [MakaMekComponent.AC20] = data => new Ac20(data),
            [MakaMekComponent.LRM5] = data => new Lrm5(data),
            [MakaMekComponent.LRM10] = data => new Lrm10(data),
            [MakaMekComponent.LRM15] = data => new Lrm15(data),
            [MakaMekComponent.LRM20] = data => new Lrm20(data),
            [MakaMekComponent.SRM2] = data => new Srm2(data),
            [MakaMekComponent.SRM4] = data => new Srm4(data),
            [MakaMekComponent.SRM6] = data => new Srm6(data),
            [MakaMekComponent.Hatchet] = data => new Hatchet(data),

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

    private static Component? CreateEngine(ComponentData? data)
    {
        if (data?.SpecificData is EngineStateData)
        {
            return new Engine(data);
        }

        return null;
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