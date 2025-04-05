using NSubstitute;
using Sanet.MakaMek.Avalonia.Views.StartNewGame;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Combat;
using Sanet.MakaMek.Core.Models.Game.Factory;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Utils.TechRules;
using Sanet.MakaMek.Core.ViewModels;
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
                Substitute.For<IRulesProvider>(),
                Substitute.For<ICommandPublisher>(),
                Substitute.For<IToHitCalculator>(),
                Substitute.For<IDispatcherService>(),
                Substitute.For<IGameFactory>());

            // Act
            view.DataContext = viewModel;

            // Assert
            view.DataContext.ShouldBe(viewModel);
        }
    }
}
