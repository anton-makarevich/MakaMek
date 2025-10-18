using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Sanet.MakaMek.Core.Services;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels;

/// <summary>
/// ViewModel for the About page that displays information about the application.
/// </summary>
public class AboutViewModel : BaseViewModel
{
    public AboutViewModel(IExternalNavigationService externalNavigationService)
    {
        var externalNavigationService1 = externalNavigationService ?? throw new ArgumentNullException(nameof(externalNavigationService));

        // Get version from entry assembly
        var assembly = GetType().Assembly;
        Version = $"v{assembly.GetName().Version?.ToString()}";

        OpenGitHubCommand = new AsyncCommand(() => externalNavigationService1.OpenUrlAsync(GitHubUrl));
        OpenMegaMekCommand = new AsyncCommand(() => externalNavigationService1.OpenUrlAsync(MegaMekUrl));
        OpenGameContentRulesCommand = new AsyncCommand(() => externalNavigationService1.OpenUrlAsync(GameContentRulesUrl));
    }

    public string Version { get; }

    public ICommand OpenGitHubCommand { get; }
    public ICommand OpenMegaMekCommand { get; }
    public ICommand OpenGameContentRulesCommand { get; }

    // URLs
    private string GitHubUrl => "https://github.com/anton-makarevich/MakaMek";
    private string MegaMekUrl => "https://megamek.org";
    private string GameContentRulesUrl => "https://www.xbox.com/en-US/developers/rules";

    // Content
    public string GameDescription => 
        "MakaMek is an open-source tactical combat game that follows Classic BattleTech rules. " +
        "The game is inspired by another computer implementation of BattleTech called MegaMek " +
        "but focusing on simplicity and accessibility for all players. We aim to keep gameplay " +
        "simple and prioritize a mobile-first and web-first user experience.";

    public string MegaMekAttribution =>
        "Some art and assets used in this project—specifically unit and terrain images—are taken from the " +
        "MegaMek Data Repository. These materials are used as-is without any modifications and are distributed " +
        "under the Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.";

    public string ContactStatement =>
        "If there are any problems using any of the material, please contact us at: anton.makarevich@gmail.com";

    public string FreeAndOpenSourceStatement =>
        "This game is free, open source, and not affiliated with any copyright or trademark holders.";

    public string TrademarkNotice1 =>
        "MechWarrior and BattleMech are registered trademarks of The Topps Company, Inc.";

    public string TrademarkNotice2 =>
        "Microsoft holds the license for MechWarrior computer games. This game is NOT affiliated with Microsoft.";

    public string GameContentRulesNotice =>
        "This game is created under Microsoft's \"Game Content Usage Rules\".";
}