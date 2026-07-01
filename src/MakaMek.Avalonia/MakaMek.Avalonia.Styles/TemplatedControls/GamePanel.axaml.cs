using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace Sanet.MakaMek.Avalonia.Styles.TemplatedControls;

public class GamePanel : ContentControl
{
    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<GamePanel, string>(
        nameof(Title));

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly StyledProperty<ICommand> CloseCommandProperty = AvaloniaProperty.Register<GamePanel, ICommand>(
        nameof(CloseCommand));

    public ICommand CloseCommand
    {
        get => GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }
}
