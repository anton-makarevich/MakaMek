using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components;

public class JumpJets : Component
{
    public static readonly EquipmentDefinition Definition = new(
        "Jump Jets",
        MakaMekComponent.JumpJet,
        0, // No battle value
        1, // 1 slot
        1, // 1 health point
        true); // Removable

    public JumpJets(int jumpMp = 1, ComponentData? componentData = null)
        : base(Definition, componentData)
    {
        // Set jump MP from component data or use provided value
        if (componentData?.SpecificData is JumpJetStateData jumpJetState)
        {
            JumpMp = jumpJetState.JumpMp;
        }
        else
        {
            JumpMp = jumpMp;
        }
    }

    public int JumpMp { get; }

    public override MakaMekComponent ComponentType => MakaMekComponent.JumpJet;

    protected override ComponentSpecificData? GetSpecificData()
    {
        return new JumpJetStateData(JumpMp);
    }
}