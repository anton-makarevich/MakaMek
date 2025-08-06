namespace Sanet.MakaMek.Core.Events;

/// <summary>
/// Type of UI event
/// </summary>
public enum UiEventType
{
    ArmorDamage,
    StructureDamage,
    Explosion,
    CriticalHit,
    ComponentDestroyed,
    LocationDestroyed,
    UnitDestroyed,
    PilotDamage,
    PilotDead,
    PilotUnconscious,
    PilotRecovered
}