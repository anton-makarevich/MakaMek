using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.Localization;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Presentation.UiStates;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MakaMek.Services;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.UiStates;

public class CombatUiStateFactoryTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly ICombatUiStateFactory _sut;

    public CombatUiStateFactoryTests()
    {
        _sut = TestCombatUiStateFactory.Create(_localizationService);
    }

    [Fact]
    public void CreatePhysicalAttackState_WhenViewModelHasNoGame_ThrowsInvalidOperationException()
    {
        // Arrange
        var viewModel = CreateViewModelWithoutGame();

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(
            () => _sut.CreatePhysicalAttackState(viewModel));
        exception.Message.ShouldBe("Game is not client game");
    }

    [Fact]
    public void CreateWeaponsAttackState_WhenViewModelHasNoGame_ThrowsInvalidOperationException()
    {
        // Arrange
        var viewModel = CreateViewModelWithoutGame();

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(
            () => _sut.CreateWeaponsAttackState(viewModel));
        exception.Message.ShouldBe("Game is not client game");
    }

    private BattleMapViewModel CreateViewModelWithoutGame() =>
        new(
            Substitute.For<IImageService>(),
            Substitute.For<ITerrainAssetService>(),
            _localizationService,
            Substitute.For<IDispatcherService>(),
            Substitute.For<IRulesProvider>(),
            Substitute.For<IPlatformService>(),
            _sut,
            Substitute.For<ICommandPublisher>(),
            Substitute.For<ILogger<BattleMapViewModel>>());
}
