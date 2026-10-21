using NSubstitute;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Map.Models;
using Sanet.Localization;
using Xunit.Abstractions;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics;

public class DiagnosticCriticalSlotTests(ITestOutputHelper output)
{
    [Fact]
    public void CriticalHitOnCenterTorsoWithOnlyHighSlotsLeft()
    {
        var rules = new TotalWarfareRulesProvider();
        var mechFactory = new MechFactory(
            rules,
            new ClassicBattletechComponentProvider(),
            Substitute.For<ILocalizationService>());

        var unitData = new UnitData
        {
            Chassis = "Test",
            Model = "Test-1",
            Mass = 20,
            EngineRating = 100,
            EngineType = "Fusion",
            ArmorValues = new Dictionary<PartLocation, ArmorLocation>
            {
                { PartLocation.Head, new ArmorLocation { FrontArmor = 9 } },
                { PartLocation.CenterTorso, new ArmorLocation { FrontArmor = 5, RearArmor = 3 } },
                { PartLocation.LeftTorso, new ArmorLocation { FrontArmor = 8, RearArmor = 4 } },
                { PartLocation.RightTorso, new ArmorLocation { FrontArmor = 8, RearArmor = 4 } },
                { PartLocation.LeftArm, new ArmorLocation { FrontArmor = 4 } },
                { PartLocation.RightArm, new ArmorLocation { FrontArmor = 4 } },
                { PartLocation.LeftLeg, new ArmorLocation { FrontArmor = 8 } },
                { PartLocation.RightLeg, new ArmorLocation { FrontArmor = 8 } }
            },
            Equipment =
            [
                new ComponentData
                {
                    Type = MakaMekComponent.Engine,
                    Assignments =
                    [
                        new LocationSlotAssignment(PartLocation.CenterTorso, 0, 3),
                        new LocationSlotAssignment(PartLocation.CenterTorso, 7, 3)
                    ],
                    SpecificData = new EngineStateData(EngineType.Fusion, 100)
                }
            ],
            AdditionalAttributes = new Dictionary<string, string>(),
            Quirks = new Dictionary<string, string>()
        };

        var unit = mechFactory.Create(unitData);
        var centerTorso = unit.Parts[PartLocation.CenterTorso];

        // Hit the first engine (slots 0-2) and the gyro (slots 3-6): only the second engine (7-9) is left.
        for (var slot = 0; slot <= 6; slot++)
        {
            centerTorso.CriticalHit(slot);
        }

        var remaining = Enumerable.Range(0, centerTorso.TotalSlots)
            .Where(slot => !centerTorso.HitSlots.Contains(slot) && centerTorso.GetComponentAtSlot(slot) != null)
            .ToList();
        output.WriteLine($"remaining slots: [{string.Join(",", remaining)}]");

        var diceRoller = Substitute.For<IDiceRoller>();
        diceRoller.Roll2D6().Returns([new DiceResult(5), new DiceResult(5)]);
        // A random d6, the same distribution the real dice roller produces.
        diceRoller.RollD6().Returns(_ => new DiceResult(Random.Shared.Next(1, 7)));

        var damageTransferCalculator = new DamageTransferCalculator(mechFactory);
        var result = unit.CalculateCriticalHitsData(
            PartLocation.CenterTorso,
            diceRoller,
            damageTransferCalculator);

        output.WriteLine($"crits: {result?.NumCriticalHits}");
    }
}
