using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components;

public class HeatSink : Component
{
    public HeatSink() : base("Heat Sink", [])
    {
        HeatDissipation = 1;
    }

    public HeatSink(int dissipation, string name) : base(name, [])
    {
        HeatDissipation = dissipation;
    }

    public int HeatDissipation { get; }

    public override void Hit()
    {
        base.Hit();
    }
    
    public override MakaMekComponent ComponentType => MakaMekComponent.HeatSink;
}
