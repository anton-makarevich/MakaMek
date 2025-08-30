using NSubstitute;
using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Utils;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Server;

public class CriticalHitsResolutionCommandTests
{
    private readonly ILocalizationService _localizationService = new FakeLocalizationService();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Unit _target;

    public CriticalHitsResolutionCommandTests()
    {
        var player =
            // Create player
            new Player(Guid.NewGuid(), "Player 1");

        // Create a target unit using MechFactory
        var mechFactory = new MechFactory(new ClassicBattletechRulesProvider(), _localizationService);
        var targetData = MechFactoryTests.CreateDummyMechData();
        targetData.Id = Guid.NewGuid();
        
        _target = mechFactory.Create(targetData);
        
        // Add unit to player
        player.AddUnit(_target);
        
        // Setup game to return players
        _game.Players.Returns(new List<IPlayer> { player });
    }

    [Fact]
    public void Render_WithSingleLocation_ShouldNotShowLocationHeader()
    {
        // Arrange
        var command = CreateCommand([
            new LocationCriticalHitsData(PartLocation.CenterTorso, [4, 4], 1,
                [new ComponentHitData
                    {
                        Slot = 0,
                        Type = MakaMekComponent.Engine
                    }
                ],
                false, [])
        ]);

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldNotContain("Critical hits in CT:");
        result.ShouldContain("Critical Roll: 8");
        result.ShouldContain("Number of critical hits: 1");
    }

    [Fact]
    public void Render_WithMultipleLocations_ShouldShowLocationHeaders()
    {
        // Arrange
        var command = CreateCommand([
            new LocationCriticalHitsData(PartLocation.CenterTorso, [4, 4], 1,
                [new ComponentHitData
                    {
                        Slot = 0,
                        Type = MakaMekComponent.Engine
                    }
                ],
                false, []),

            new LocationCriticalHitsData(PartLocation.LeftArm, [3, 3], 1,
                [new ComponentHitData
                    {
                        Slot = 1,
                        Type = MakaMekComponent.MediumLaser
                    }
                ],
                false, [])
        ]);

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Critical hits in CT:");
        result.ShouldContain("Critical hits in LA:");
    }

    [Fact]
    public void Render_WithBlownOffLocation_ShouldShowBlownOffMessageAndSkipOtherDetails()
    {
        // Arrange
        var command = CreateCommand([
            new LocationCriticalHitsData(PartLocation.LeftArm, [6, 6], 0, null, true, [])]);

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Critical Roll: 12");
        result.ShouldContain("Critical hit in LA, location blown off");
        result.ShouldNotContain("Number of critical hits:");
        result.ShouldNotContain("Critical hit in LA slot");
    }

    [Fact]
    public void Render_WithNonExistentComponentSlot_ShouldSkipThatComponent()
    {
        // Arrange
        var command = CreateCommand([
            new LocationCriticalHitsData(PartLocation.CenterTorso, [4, 4], 2,
                [
                    new ComponentHitData { Slot = 0, Type = MakaMekComponent.Engine }, // Valid slot
                    new ComponentHitData { Slot = 99, Type = MakaMekComponent.MediumLaser } // Invalid slot
                ],
                false, [])
        ]);

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Critical Roll: 8");
        result.ShouldContain("Number of critical hits: 2");
        // Should only show the valid component hit
        result.ShouldContain("Critical hit in CT slot 1:");
        // Should not show the invalid component hit
        result.ShouldNotContain("slot 100:");
    }

    [Fact]
    public void Render_WithExplosionFromNonExistentComponent_ShouldSkipExplosion()
    {
        // Arrange
        var command = CreateCommand([
            new LocationCriticalHitsData(PartLocation.CenterTorso, [4, 4], 1,
                [new ComponentHitData { Slot = 0, Type = MakaMekComponent.Engine }],
                false,
                [])
        ]);

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Critical Roll: 8");
        result.ShouldContain("Number of critical hits: 1");
        result.ShouldNotContain("exploded");
    }

    [Fact]
    public void Render_WithInvalidTargetId_ShouldReturnEmptyString()
    {
        // Arrange
        var command = new CriticalHitsResolutionCommand
        {
            GameOriginId = _gameId,
            TargetId = Guid.NewGuid(), // Invalid target ID
            CriticalHits = []
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Render_WithComplexScenario_ShouldFormatCorrectly()
    {
        // Arrange - Multiple locations, some with crits, some blown off, some with explosions
        var rightArm = _target.Parts.First(p => p.Location == PartLocation.RightArm);
        var ammo = new Ammo(Lrm5.Definition, 20);
        rightArm.TryAddComponent(ammo).ShouldBeTrue();

        var command = CreateCommand([
            new LocationCriticalHitsData(PartLocation.CenterTorso, [4, 4], 1,
                [new ComponentHitData { Slot = 0, Type = MakaMekComponent.Engine }],
                false,
                []),

            new LocationCriticalHitsData(PartLocation.LeftArm, [6, 6], 0, null, true, []),
            new LocationCriticalHitsData(PartLocation.RightArm, [3, 3], 2,
                [
                    new ComponentHitData { Slot = 1, Type = MakaMekComponent.MediumLaser },
                    new ComponentHitData { Slot = ammo.MountedAtSlots[0], Type = ammo.ComponentType }
                ],
                false, [])
        ]);

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();

        // Should show location headers for multiple locations
        result.ShouldContain("Critical hits in CT:");
        result.ShouldContain("Critical hits in LA:");
        result.ShouldContain("Critical hits in RA:");

        // Should show blown off for LeftArm
        result.ShouldContain("Critical hit in LA, location blown off");



        // Should show multiple critical hits for RightArm
        result.ShouldContain("Number of critical hits: 2");
        // Should show explosion in RightArm
        result.ShouldContain("exploded, damage: 100");
    }

    private CriticalHitsResolutionCommand CreateCommand(List<LocationCriticalHitsData> criticalHits)
    {
        return new CriticalHitsResolutionCommand
        {
            GameOriginId = _gameId,
            TargetId = _target.Id,
            CriticalHits = criticalHits
        };
    }
}
