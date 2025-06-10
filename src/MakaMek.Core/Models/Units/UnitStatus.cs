namespace Sanet.MakaMek.Core.Models.Units;

[System.Flags]
public enum UnitStatus
{
    None = 0,
    Active = 1,
    Shutdown = 2,
    Prone = 4,
    Immobile = 8,
    Destroyed = 32
}
