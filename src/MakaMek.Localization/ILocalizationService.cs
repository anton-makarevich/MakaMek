namespace Sanet.MakaMek.Localization;

public interface ILocalizationService
{
    string GetString(string key);

    /// <summary>
    /// Raised when the active language changes, so consumers can re-resolve localized strings.
    /// </summary>
    event EventHandler? LanguageChanged;
}
