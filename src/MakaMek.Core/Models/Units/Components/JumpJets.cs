using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components;

public class JumpJets : Component
{
    public static readonly EquipmentDefinition Definition = new(
        "Jump Jets",
        MakaMekComponent.JumpJet); // Removable

    public JumpJets(ComponentData? componentData = null)
        : base(Definition, componentData)
    {
        // Each jump jet always provides exactly 1 MP as per BattleTech rules
        JumpMp = 1;
    }

    public int JumpMp { get; }
}