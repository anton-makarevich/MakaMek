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

    public JumpJets(ComponentData? componentData = null)
        : base(Definition, componentData)
    {
        // Each jump jet always provides exactly 1 MP as per BattleTech rules
        JumpMp = 1;
    }

    public int JumpMp { get; }

    public override MakaMekComponent ComponentType => MakaMekComponent.JumpJet;

    // No specific data needed since JumpMp is always 1
}