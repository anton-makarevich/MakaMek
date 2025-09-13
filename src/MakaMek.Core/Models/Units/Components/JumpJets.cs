using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components;

public class JumpJets : Component
{
    public JumpJets() : this(1)
    {
    }

    public JumpJets(int jumpMp) : base("Jump Jets")
    {
        JumpMp = jumpMp;
    }

    public int JumpMp { get; }
    
    public override MakaMekComponent ComponentType => MakaMekComponent.JumpJet;
}