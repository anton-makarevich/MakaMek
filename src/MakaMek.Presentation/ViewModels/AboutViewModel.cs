using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels;

/// <summary>
/// ViewModel for the About page that displays information about the application.
/// </summary>
public class AboutViewModel : BaseViewModel
{
    private readonly ILocalizationService _localizationService;

    public AboutViewModel(IExternalNavigationService externalNavigationService, ILocalizationService localizationService)
    {
        var externalNavigationService1 = externalNavigationService ?? throw new ArgumentNullException(nameof(externalNavigationService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));

        // Get version from entry assembly
        var assembly = GetType().Assembly;
        Version = $"v{assembly.GetName().Version?.ToString()}";

        OpenGitHubCommand = new AsyncCommand(() => externalNavigationService1.OpenUrlAsync(GitHubUrl));
        OpenMegaMekCommand = new AsyncCommand(() => externalNavigationService1.OpenUrlAsync(MegaMekUrl));
        OpenGameContentRulesCommand = new AsyncCommand(() => externalNavigationService1.OpenUrlAsync(GameContentRulesUrl));
        OpenContactEmailCommand = new AsyncCommand(() => externalNavigationService1.OpenEmailAsync(ContactEmail, $"MakaMek {Version} question"));
    }

    public string Version { get; }

    public ICommand OpenGitHubCommand { get; }
    public ICommand OpenMegaMekCommand { get; }
    public ICommand OpenGameContentRulesCommand { get; }
    public ICommand OpenContactEmailCommand { get; }

    // URLs
    private string GitHubUrl => "https://github.com/anton-makarevich/MakaMek";
    private string MegaMekUrl => "https://megamek.org";
    private string GameContentRulesUrl => "https://www.xbox.com/en-US/developers/rules";
    private string ContactEmail => "anton.makarevich@gmail.com";

    // Content
    public string GameDescription => _localizationService.GetString("About_GameDescription");
    public string MegaMekAttribution => _localizationService.GetString("About_MegaMekAttribution");
    public string ContactStatement => _localizationService.GetString("About_ContactStatement");
    public string FreeAndOpenSourceStatement => _localizationService.GetString("About_FreeAndOpenSourceStatement");
    public string TrademarkNotice1 => _localizationService.GetString("About_TrademarkNotice1");
    public string TrademarkNotice2 => _localizationService.GetString("About_TrademarkNotice2");
    public string GameContentRulesNotice => _localizationService.GetString("About_GameContentRulesNotice");
}