using Sanet.MakaMek.Core.Data.Community;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons;

/// <summary>
/// Central repository of all weapon definitions in the game.
/// This allows sharing definitions between weapon and ammo classes.
/// </summary>
public static class WeaponDefinitions
{
    // Energy weapons
    public static readonly WeaponDefinition MediumLaser = new(
        name: "Medium Laser",
        elementaryDamage: 5,
        heat: 3,
        minimumRange: 0,
        shortRange: 3,
        mediumRange: 6,
        longRange: 9,
        type: WeaponType.Energy,
        battleValue: 46,
        weaponComponentType: MakaMekComponent.MediumLaser);
        
    public static readonly WeaponDefinition LargeLaser = new(
        name: "Large Laser",
        elementaryDamage: 8,
        heat: 8,
        minimumRange: 0,
        shortRange: 5,
        mediumRange: 10,
        longRange: 15,
        type: WeaponType.Energy,
        battleValue: 100,
        weaponComponentType: MakaMekComponent.LargeLaser);
        
    public static readonly WeaponDefinition SmallLaser = new(
        name: "Small Laser",
        elementaryDamage: 3,
        heat: 1,
        minimumRange: 0,
        shortRange: 1,
        mediumRange: 2,
        longRange: 3,
        type: WeaponType.Energy,
        battleValue: 9,
        weaponComponentType: MakaMekComponent.SmallLaser);
        
    public static readonly WeaponDefinition PPC = new(
        name: "PPC",
        elementaryDamage: 10,
        heat: 10,
        minimumRange: 3,
        shortRange: 6,
        mediumRange: 12,
        longRange: 18,
        type: WeaponType.Energy,
        battleValue: 175,
        weaponComponentType: MakaMekComponent.PPC);
        
    // Ballistic weapons
    public static readonly WeaponDefinition MachineGun = new(
        name: "Machine Gun",
        elementaryDamage: 2,
        heat: 0,
        minimumRange: 0,
        shortRange: 1,
        mediumRange: 2,
        longRange: 3,
        type: WeaponType.Ballistic,
        battleValue: 5,
        weaponComponentType: MakaMekComponent.MachineGun,
        ammoComponentType: MakaMekComponent.ISAmmoMG);
        
    public static readonly WeaponDefinition AC2 = new(
        name: "AC/2",
        elementaryDamage: 2,
        heat: 1,
        minimumRange: 4,
        shortRange: 8,
        mediumRange: 16,
        longRange: 24,
        type: WeaponType.Ballistic,
        battleValue: 37,
        weaponComponentType: MakaMekComponent.AC2,
        ammoComponentType: MakaMekComponent.ISAmmoAC2);
        
    public static readonly WeaponDefinition AC5 = new(
        name: "AC/5",
        elementaryDamage: 5,
        heat: 1,
        minimumRange: 3,
        shortRange: 6,
        mediumRange: 12,
        longRange: 18,
        type: WeaponType.Ballistic,
        battleValue: 70,
        weaponComponentType: MakaMekComponent.AC5,
        ammoComponentType: MakaMekComponent.ISAmmoAC5);
        
    public static readonly WeaponDefinition AC10 = new(
        name: "AC/10",
        elementaryDamage: 10,
        heat: 3,
        minimumRange: 0,
        shortRange: 5,
        mediumRange: 10,
        longRange: 15,
        type: WeaponType.Ballistic,
        battleValue: 110,
        weaponComponentType: MakaMekComponent.AC10,
        ammoComponentType: MakaMekComponent.ISAmmoAC10);
        
    public static readonly WeaponDefinition AC20 = new(
        name: "AC/20",
        elementaryDamage: 20,
        heat: 7,
        minimumRange: 0,
        shortRange: 3,
        mediumRange: 6,
        longRange: 9,
        type: WeaponType.Ballistic,
        battleValue: 178,
        weaponComponentType: MakaMekComponent.AC20,
        ammoComponentType: MakaMekComponent.ISAmmoAC20);
        
    // Missile weapons
    public static readonly WeaponDefinition LRM5 = new(
        name: "LRM-5",
        elementaryDamage: 1,
        heat: 2,
        minimumRange: 6,
        shortRange: 7,
        mediumRange: 14,
        longRange: 21,
        type: WeaponType.Missile,
        battleValue: 45,
        clusters: 1,
        clusterSize: 5,
        weaponComponentType: MakaMekComponent.LRM5,
        ammoComponentType: MakaMekComponent.ISAmmoLRM5);
        
    public static readonly WeaponDefinition LRM10 = new(
        name: "LRM-10",
        elementaryDamage: 1,
        heat: 4,
        minimumRange: 6,
        shortRange: 7,
        mediumRange: 14,
        longRange: 21,
        type: WeaponType.Missile,
        battleValue: 90,
        clusters: 2,
        clusterSize: 5,
        weaponComponentType: MakaMekComponent.LRM10,
        ammoComponentType: MakaMekComponent.ISAmmoLRM10);
        
    public static readonly WeaponDefinition LRM15 = new(
        name: "LRM-15",
        elementaryDamage: 1,
        heat: 5,
        minimumRange: 6,
        shortRange: 7,
        mediumRange: 14,
        longRange: 21,
        type: WeaponType.Missile,
        battleValue: 135,
        clusters: 3,
        clusterSize: 5,
        weaponComponentType: MakaMekComponent.LRM15,
        ammoComponentType: MakaMekComponent.ISAmmoLRM15);
        
    public static readonly WeaponDefinition LRM20 = new(
        name: "LRM-20",
        elementaryDamage: 1,
        heat: 6,
        minimumRange: 6,
        shortRange: 7,
        mediumRange: 14,
        longRange: 21,
        type: WeaponType.Missile,
        battleValue: 180,
        clusters: 4,
        clusterSize: 5,
        weaponComponentType: MakaMekComponent.LRM20,
        ammoComponentType: MakaMekComponent.ISAmmoLRM20);
        
    public static readonly WeaponDefinition SRM2 = new(
        name: "SRM-2",
        elementaryDamage: 2,
        heat: 2,
        minimumRange: 0,
        shortRange: 3,
        mediumRange: 6,
        longRange: 9,
        type: WeaponType.Missile,
        battleValue: 15,
        clusters: 1,
        clusterSize: 2,
        weaponComponentType: MakaMekComponent.SRM2,
        ammoComponentType: MakaMekComponent.ISAmmoSRM2);
        
    public static readonly WeaponDefinition SRM4 = new(
        name: "SRM-4",
        elementaryDamage: 2,
        heat: 3,
        minimumRange: 0,
        shortRange: 3,
        mediumRange: 6,
        longRange: 9,
        type: WeaponType.Missile,
        battleValue: 30,
        clusters: 1,
        clusterSize: 4,
        weaponComponentType: MakaMekComponent.SRM4,
        ammoComponentType: MakaMekComponent.ISAmmoSRM4);
        
    public static readonly WeaponDefinition SRM6 = new(
        name: "SRM-6",
        elementaryDamage: 2,
        heat: 4,
        minimumRange: 0,
        shortRange: 3,
        mediumRange: 6,
        longRange: 9,
        type: WeaponType.Missile,
        battleValue: 45,
        clusters: 1,
        clusterSize: 6,
        weaponComponentType: MakaMekComponent.SRM6,
        ammoComponentType: MakaMekComponent.ISAmmoSRM6);
}
