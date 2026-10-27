namespace MakaMek.Tools.MW5Extractor.Discovery;

/// <summary>What a MechWarrior 5 asset is, as far as the render pipeline cares.</summary>
public enum Mw5AssetKind
{
    /// <summary>Candidate chassis body: a mesh at <c>Model/Body/</c> depth 1. The report picks one per chassis.</summary>
    BodyMesh,

    /// <summary>Replacement or modular geometry under <c>Model/Body</c> (arm/limb/torso swaps, modular parts).</summary>
    BodyPart,

    /// <summary>Destroyed-state geometry: <c>…_DM0/_DM1/_DM2/_Debris_STM</c>.</summary>
    BodyDamage,

    /// <summary>The separate cockpit skeletal mesh, <c>Model/Cockpit/&lt;Chassis&gt;_Cockpit_SKM</c>.</summary>
    Cockpit,

    /// <summary>Cockpit glass, <c>Model/Cockpit/&lt;Chassis&gt;_CockpitGlass_STM</c>.</summary>
    CockpitGlass,

    /// <summary>A weapon mesh (or its animation/physics sidecar), <c>Model/Weapons/Weapon_Mech_&lt;Chassis&gt;_…</c>.</summary>
    Weapon,

    /// <summary>The hardpoint set, <c>Model/&lt;Chassis&gt;_HPS</c> (custom class: not readable through CUE4Parse).</summary>
    HardpointSet,

    /// <summary>The mech model definition, <c>Model/&lt;Chassis&gt;_MDL</c> (custom class).</summary>
    ModelDefinition,

    /// <summary>Per-variant data asset, <c>MDA/&lt;Model&gt;_MDA</c> (custom class).</summary>
    MechData,

    /// <summary>Per-variant loadout, <c>Loadout/&lt;Model&gt;_Loadout</c> (custom class).</summary>
    Loadout,

    /// <summary>An animation sequence, <c>Animation/…_ANI</c>.</summary>
    Animation,

    /// <summary>Paint scheme, <c>Skins/…_SKN</c>.</summary>
    Skin,

    /// <summary>The paper-doll unit card art, <c>UnitCard/…</c>.</summary>
    UnitCard,

    /// <summary>Anything else under the mech tree (materials, textures, audio, decals, …).</summary>
    Other
}
