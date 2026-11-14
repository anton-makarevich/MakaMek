using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Data.Community;

/// <summary>
/// Maps the textual location and equipment names used by MTF files onto the game's enums.
/// </summary>
internal static class MtfComponentMapping
{
    public static PartLocation ParseLocation(string locationText) =>
        locationText switch
        {
            "Left Arm" or "LA" => PartLocation.LeftArm,
            "Right Arm" or "RA" => PartLocation.RightArm,
            "Left Torso" or "LT" => PartLocation.LeftTorso,
            "Right Torso" or "RT" => PartLocation.RightTorso,
            "Center Torso" or "CT" => PartLocation.CenterTorso,
            "Head" or "HD" => PartLocation.Head,
            "Left Leg" or "LL" => PartLocation.LeftLeg,
            "Right Leg" or "RL" => PartLocation.RightLeg,
            "RTL" or "RTR" or "RTC" => GetMainLocationForRear(locationText),
            _ => throw new ArgumentException($"Unknown location: {locationText}")
        };

    public static bool TryParseLocation(string locationText, out PartLocation location)
    {
        location = ParseLocation(locationText);
        return true;
    }

    public static bool IsRearArmor(string locationText) =>
        locationText is "RTL" or "RTR" or "RTC";

    public static PartLocation GetMainLocationForRear(string rearLocationText) => rearLocationText switch
    {
        "RTL" => PartLocation.LeftTorso,
        "RTR" => PartLocation.RightTorso,
        "RTC" => PartLocation.CenterTorso,
        _ => throw new ArgumentException($"Invalid rear location: {rearLocationText}")
    };

    public static MakaMekComponent MapMtfStringToComponent(string mtfString) =>
        mtfString switch
        {
            "IS Ammo AC/2" => MakaMekComponent.ISAmmoAC2,
            "IS Ammo AC/5" => MakaMekComponent.ISAmmoAC5,
            "IS Ammo AC/10" => MakaMekComponent.ISAmmoAC10,
            "IS Ammo AC/20" => MakaMekComponent.ISAmmoAC20,
            "IS Ammo SRM-2" => MakaMekComponent.ISAmmoSRM2,
            "IS Ammo SRM-4" => MakaMekComponent.ISAmmoSRM4,
            "IS Ammo SRM-6" => MakaMekComponent.ISAmmoSRM6,
            "IS Ammo MG" or "IS Machine Gun Ammo" => MakaMekComponent.ISAmmoMG,
            "IS Ammo LRM-5" => MakaMekComponent.ISAmmoLRM5,
            "IS Ammo LRM-10" => MakaMekComponent.ISAmmoLRM10,
            "IS Ammo LRM-15" => MakaMekComponent.ISAmmoLRM15,
            "IS Ammo LRM-20" => MakaMekComponent.ISAmmoLRM20,
            "Small Laser" or "ISSmallLaser" => MakaMekComponent.SmallLaser,
            "Medium Laser" or "ISMediumLaser" => MakaMekComponent.MediumLaser,
            "Large Laser" => MakaMekComponent.LargeLaser,
            "PPC" => MakaMekComponent.PPC,
            "Flamer" => MakaMekComponent.Flamer,
            "LRM 5" => MakaMekComponent.LRM5,
            "LRM 10" => MakaMekComponent.LRM10,
            "LRM 15" => MakaMekComponent.LRM15,
            "LRM 20" => MakaMekComponent.LRM20,
            "SRM 2" => MakaMekComponent.SRM2,
            "SRM 4" => MakaMekComponent.SRM4,
            "SRM 6" => MakaMekComponent.SRM6,
            "Machine Gun" => MakaMekComponent.MachineGun,
            "Autocannon/2" => MakaMekComponent.AC2,
            "Autocannon/5" => MakaMekComponent.AC5,
            "Autocannon/10" => MakaMekComponent.AC10,
            "Autocannon/20" => MakaMekComponent.AC20,
            "Heat Sink" => MakaMekComponent.HeatSink,
            "Shoulder" => MakaMekComponent.Shoulder,
            "Upper Arm Actuator" => MakaMekComponent.UpperArmActuator,
            "Lower Arm Actuator" => MakaMekComponent.LowerArmActuator,
            "Hand Actuator" => MakaMekComponent.HandActuator,
            "Jump Jet" => MakaMekComponent.JumpJet,
            "Fusion Engine" => MakaMekComponent.Engine,
            "Gyro" => MakaMekComponent.Gyro,
            "Life Support" => MakaMekComponent.LifeSupport,
            "Sensors" => MakaMekComponent.Sensors,
            "Cockpit" => MakaMekComponent.Cockpit,
            "Hip" => MakaMekComponent.Hip,
            "Upper Leg Actuator" => MakaMekComponent.UpperLegActuator,
            "Lower Leg Actuator" => MakaMekComponent.LowerLegActuator,
            "Foot Actuator" => MakaMekComponent.FootActuator,
            _ => throw new NotImplementedException($"Unknown MTF component: {mtfString}")
        };
}
