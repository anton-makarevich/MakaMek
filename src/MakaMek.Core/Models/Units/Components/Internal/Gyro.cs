using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal;

public class Gyro() : Component("Gyro",healthPoints:2)
{
    public override MakaMekComponent ComponentType => MakaMekComponent.Gyro;
    public override bool IsRemovable => false;
}
