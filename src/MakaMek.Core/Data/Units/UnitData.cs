using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Data.Units;

public record struct UnitData
{
    public Guid? Id { get; set; } 
    public required string Chassis { get; init; }
    public required string Model { get; init; }
    public required int Mass { get; init; }
    public required int WalkMp { get; init; }
    public required int EngineRating { get; init; }
    public required string EngineType { get; init; }
    public required Dictionary<PartLocation, ArmorLocation> ArmorValues { get; init; }
    public required Dictionary<PartLocation, List<MakaMekComponent>> LocationEquipment { get; init; }
    public required Dictionary<string, string> AdditionalAttributes { get; init; }
    public required Dictionary<string,string> Quirks { get; init; }
}