using NSubstitute;
using Sanet.MakaMek.Avalonia.Views.StartNewGame;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Map.Factory;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.TechRules;
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
                Substitute.For<IBattleMapFactory>());

            // Act
            view.DataContext = viewModel;

            // Assert
            view.DataContext.ShouldBe(viewModel);
        }
    }
}
