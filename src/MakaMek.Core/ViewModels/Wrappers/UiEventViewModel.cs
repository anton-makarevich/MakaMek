using Sanet.MakaMek.Core.Events;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.ViewModels.Wrappers
{
    /// <summary>
    /// View model for UI events
    /// </summary>
    public class UiEventViewModel
    {
        private readonly UiEvent _event;
        private readonly ILocalizationService _localizationService;

        /// <summary>
        /// Creates a new UI event view model
        /// </summary>
        /// <param name="uiEvent">The UI event to wrap</param>
        /// <param name="localizationService">Localization service for formatting text</param>
        public UiEventViewModel(UiEvent uiEvent, ILocalizationService localizationService)
        {
            _event = uiEvent;
            _localizationService = localizationService;
        }

        /// <summary>
        /// The type of event
        /// </summary>
        public UiEventType Type => _event.Type;

        /// <summary>
        /// Formatted text for display
        /// </summary>
        public string FormattedText
        {
            get
            {
                var template = _localizationService.GetString($"Events_Unit_{_event.Type}");
                return string.Format(template, _event.Parameters);
            }
        }
    }
}
