using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace Sanet.MakaMek.Avalonia.Controls.TemplatedControls;

/// <summary>
/// Provides a consistent, closable overlay panel for in-game tools and information.
/// </summary>
public class GamePanel : ContentControl
{
    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<GamePanel, string>(
        nameof(Title));

    /// <summary>
    /// Gets or sets the text shown in the panel header.
    /// </summary>
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly StyledProperty<ICommand> CloseCommandProperty = AvaloniaProperty.Register<GamePanel, ICommand>(
        nameof(CloseCommand));

    /// <summary>
    /// Gets or sets the command invoked by the header close button.
    /// </summary>
    public ICommand CloseCommand
    {
        get => GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }
}
