﻿namespace Sanet.MakaMek.Core.Models.Units.Mechs;

using Components.Internal.Actuators;

public class Leg : UnitPart
{
    public Leg(PartLocation location, int maxArmor, int maxStructure) 
        : base($"{location} Leg", location, maxArmor, maxStructure, 12)
    {
        // Add default components
        TryAddComponent(new Hip());
        TryAddComponent(new UpperLegActuator());
        TryAddComponent(new LowerLegActuator());
        TryAddComponent(new FootActuator());
    }
}