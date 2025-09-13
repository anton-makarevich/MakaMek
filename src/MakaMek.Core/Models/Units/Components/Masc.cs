using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components;

public class Masc : Component
{
    public Masc(string name) : base(name)
    {
        Deactivate(); // MASC starts deactivated
    }

    public override MakaMekComponent ComponentType=> MakaMekComponent.Masc;

    public override void Hit()
    {
        base.Hit();
        Deactivate();
    }
}