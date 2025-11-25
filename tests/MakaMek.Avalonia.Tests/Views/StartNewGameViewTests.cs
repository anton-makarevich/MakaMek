using NSubstitute;
using Sanet.MakaMek.Avalonia.Views.StartNewGame;
using Sanet.MakaMek.Bots.Models;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map.Factory;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Cryptography;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Presentation.ViewModels;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Views
{
    public class StartNewGameViewTests
    {
        [Fact]
        public void NewGameView_WhenCreated_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var view = new StartNewGameViewNarrow();

            // Assert
            view.ShouldNotBeNull();
        }

        [Fact]
        public void NewGameView_WhenViewModelSet_ShouldBindCorrectly()
        {
            // Arrange
            var view = new StartNewGameViewNarrow();
            var cachingService = Substitute.For<IFileCachingService>();
            cachingService.TryGetCachedFile(Arg.Any<string>()).Returns(Task.FromResult<byte[]?>(null));

            var viewModel = new StartNewGameViewModel(
                Substitute.For<IGameManager>(),
                Substitute.For<IUnitsLoader>(),
                Substitute.For<IRulesProvider>(),
                Substitute.For<IMechFactory>(),
                Substitute.For<ICommandPublisher>(),
                Substitute.For<IToHitCalculator>(),
                Substitute.For<IPilotingSkillCalculator>(),
                Substitute.For<IConsciousnessCalculator>(),
                Substitute.For<IHeatEffectsCalculator>(),
                Substitute.For<IDispatcherService>(),
                Substitute.For<IGameFactory>(),
                Substitute.For<IBattleMapFactory>(),
                cachingService,
                Substitute.For<IMapPreviewRenderer>(),
                Substitute.For<IHashService>(),
                Substitute.For<IBotManager>());

            // Act
            view.DataContext = viewModel;

            // Assert
            view.DataContext.ShouldBe(viewModel);
        }
    }
}
