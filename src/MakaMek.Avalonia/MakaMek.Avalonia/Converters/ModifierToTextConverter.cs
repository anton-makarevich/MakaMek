using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Avalonia.Converters;

public class ModifierToTextConverter : IValueConverter
{
    private static ILocalizationService? _localizationService;
    
    public static void Initialize(ILocalizationService localization)
    {
        _localizationService = localization;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not RollModifier modifier || _localizationService == null)
            return string.Empty;
            
        return modifier.Render(_localizationService);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
