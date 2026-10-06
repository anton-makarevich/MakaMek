using Sanet.MakaMek.Core.Services.Transport.Relay;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Services.Transport.Relay;

public class RelayGameInfoFactoryTests
{
    [Fact]
    public void Create_UsesTheGivenHostGameId()
    {
        // Arrange
        var hostGameId = Guid.NewGuid();

        // Act
        var gameInfo = RelayGameInfoFactory.Create(hostGameId);

        // Assert
        gameInfo.HostId.ShouldBe(hostGameId);
    }

    [Fact]
    public void Create_ReportsTheMakaMekTitle()
    {
        // Act
        var gameInfo = RelayGameInfoFactory.Create(Guid.NewGuid());

        // Assert
        gameInfo.Id.ShouldBe(RelayGameInfoFactory.GameTitle);
        gameInfo.Id.ShouldBe("MakaMek");
    }

    [Fact]
    public void Create_OmitsMetadata()
    {
        // Act
        var gameInfo = RelayGameInfoFactory.Create(Guid.NewGuid());

        // Assert - the ticket does not call for game-specific attributes
        gameInfo.Metadata.ShouldBeNull();
    }

    [Fact]
    public void GameVersion_IsMajorMinorBuildOfThisAssembly()
    {
        // Act
        var version = RelayGameInfoFactory.GameVersion;

        // Assert
        var assemblyVersion = typeof(RelayGameInfoFactory).Assembly.GetName().Version!;
        version.ShouldBe($"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}");
    }

    [Fact]
    public void GameVersion_SatisfiesHubValidationRules()
    {
        // Act - the hub rejects versions longer than 32 characters, with control characters,
        // or with surrounding whitespace, so a version that violates those rules is a bug
        var version = RelayGameInfoFactory.GameVersion;

        // Assert
        version.ShouldNotBeNullOrWhiteSpace();
        version.ShouldBe(version.Trim());
        version.Length.ShouldBeInRange(1, 32);
        version.ShouldNotContain(controlChar => char.IsControl(controlChar));
    }

    [Fact]
    public void GameTitle_SatisfiesHubValidationRules()
    {
        // Act - the hub only accepts letters, digits, '.', '_' and '-' in a game title
        const string title = RelayGameInfoFactory.GameTitle;

        // Assert
        title.Length.ShouldBeInRange(1, 64);
        title.ShouldAllBe(character =>
            char.IsAsciiLetterOrDigit(character) || AllowedTitleCharacters.Contains(character));
    }

    private const string AllowedTitleCharacters = "._-";
}
