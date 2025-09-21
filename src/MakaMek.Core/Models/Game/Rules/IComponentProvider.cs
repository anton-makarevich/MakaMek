using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components;

namespace Sanet.MakaMek.Core.Models.Game.Rules;

/// <summary>
/// Interface for component definition registry
/// </summary>
public interface IComponentProvider
{
    ComponentDefinition? GetDefinition(MakaMekComponent componentType, ComponentSpecificData? specificData = null);
    Component? CreateComponent(MakaMekComponent componentType, ComponentData? componentData = null);
}