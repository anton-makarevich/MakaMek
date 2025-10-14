using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;

namespace Sanet.MakaMek.Avalonia.Views.TemplatedControls;

public class ToolBar : TemplatedControl
{
    public static readonly StyledProperty<ICommand> LeftCommandProperty =
        AvaloniaProperty.Register<ToolBar, ICommand>(nameof(LeftCommand));

    public static readonly StyledProperty<string> LeftIconProperty = AvaloniaProperty.Register<ToolBar, string>(
        nameof(LeftIcon));

    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<ToolBar, string>(
        nameof(Title));

    public static readonly StyledProperty<ICommand> RightCommandProperty = AvaloniaProperty.Register<ToolBar, ICommand>(
        nameof(RightCommand));

    public static readonly StyledProperty<string> RightIconProperty = AvaloniaProperty.Register<ToolBar, string>(
        nameof(RightIcon));

    public static readonly StyledProperty<HorizontalAlignment> TitleAlignmentProperty = AvaloniaProperty.Register<ToolBar, HorizontalAlignment>(
        nameof(TitleAlignment));

    public HorizontalAlignment TitleAlignment
    {
        get => GetValue(TitleAlignmentProperty);
        set => SetValue(TitleAlignmentProperty, value);
    }
    
    public string RightIcon
    {
        get => GetValue(RightIconProperty);
        set => SetValue(RightIconProperty, value);
    }
    
    public ICommand RightCommand
    {
        get => GetValue(RightCommandProperty);
        set => SetValue(RightCommandProperty, value);
    }
    
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string LeftIcon
    {
        get => GetValue(LeftIconProperty);
        set => SetValue(LeftIconProperty, value);
    }

    public ICommand LeftCommand
    {
        get => GetValue(LeftCommandProperty);
        set => SetValue(LeftCommandProperty, value);
    }

    public bool HasLeftButton => !string.IsNullOrEmpty(LeftIcon);
    public bool HasRightButton => !string.IsNullOrEmpty(RightIcon);
}