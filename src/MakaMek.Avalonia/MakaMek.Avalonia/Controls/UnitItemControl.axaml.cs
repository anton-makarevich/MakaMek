using System;
using System.Threading.Tasks;
using System.Windows.Input;
using AsyncAwaitBestPractices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Sanet.MakaMek.Services;

namespace Sanet.MakaMek.Avalonia.Controls;

public partial class UnitItemControl : UserControl
{
    private int _requestVersion;

    public static readonly StyledProperty<ICommand?> RemoveCommandProperty =
        AvaloniaProperty.Register<UnitItemControl, ICommand?>(nameof(RemoveCommand));

    public static readonly StyledProperty<object?> RemoveCommandParameterProperty =
        AvaloniaProperty.Register<UnitItemControl, object?>(nameof(RemoveCommandParameter));

    public static readonly StyledProperty<bool> IsRemoveButtonVisibleProperty =
        AvaloniaProperty.Register<UnitItemControl, bool>(nameof(IsRemoveButtonVisible), defaultValue: false);

    public static readonly StyledProperty<ICommand?> InfoCommandProperty =
        AvaloniaProperty.Register<UnitItemControl, ICommand?>(nameof(InfoCommand));

    public static readonly StyledProperty<object?> InfoCommandParameterProperty =
        AvaloniaProperty.Register<UnitItemControl, object?>(nameof(InfoCommandParameter));

    public static readonly StyledProperty<bool> CanEditUnitNameProperty =
        AvaloniaProperty.Register<UnitItemControl, bool>(nameof(CanEditUnitName), defaultValue: true);

    public static readonly StyledProperty<string?> PlayerTintProperty =
        AvaloniaProperty.Register<UnitItemControl, string?>(nameof(PlayerTint));

    /// <summary>
    /// Image service the card loads its preview from. The host view binds it from the screen
    /// ViewModel, so the control declares its dependency instead of locating one.
    /// </summary>
    public static readonly StyledProperty<IImageService?> ImageServiceProperty =
        AvaloniaProperty.Register<UnitItemControl, IImageService?>(nameof(ImageService));

    static UnitItemControl()
    {
        // Class handlers are registered once per type: a change on any card reloads that card only.
        PlayerTintProperty.Changed.AddClassHandler<UnitItemControl>((control, _) => control.ReloadImage());
        ImageServiceProperty.Changed.AddClassHandler<UnitItemControl>((control, _) => control.ReloadImage());
    }

    public ICommand? InfoCommand
    {
        get => GetValue(InfoCommandProperty);
        set => SetValue(InfoCommandProperty, value);
    }

    public object? InfoCommandParameter
    {
        get => GetValue(InfoCommandParameterProperty);
        set => SetValue(InfoCommandParameterProperty, value);
    }

    public ICommand? RemoveCommand
    {
        get => GetValue(RemoveCommandProperty);
        set => SetValue(RemoveCommandProperty, value);
    }

    public object? RemoveCommandParameter
    {
        get => GetValue(RemoveCommandParameterProperty);
        set => SetValue(RemoveCommandParameterProperty, value);
    }

    public bool IsRemoveButtonVisible
    {
        get => GetValue(IsRemoveButtonVisibleProperty);
        set => SetValue(IsRemoveButtonVisibleProperty, value);
    }

    public string? PlayerTint
    {
        get => GetValue(PlayerTintProperty);
        set => SetValue(PlayerTintProperty, value);
    }

    public bool CanEditUnitName
    {
        get => GetValue(CanEditUnitNameProperty);
        set => SetValue(CanEditUnitNameProperty, value);
    }

    public IImageService? ImageService
    {
        get => GetValue(ImageServiceProperty);
        set => SetValue(ImageServiceProperty, value);
    }

    public UnitItemControl()
    {
        InitializeComponent();
        // Always listen: the service and the card's unit may arrive in either order.
        DataContextChanged += (_, _) => ReloadImage();
    }

    private void ReloadImage()
    {
        // Bumped per request so an image that arrives after the card changed hands is discarded.
        var version = ++_requestVersion;

        if (DataContext is not UnitViewModel unitVm || ImageService is not { } service)
        {
            Apply(null, null);
            return;
        }

        var imageService = service as IImageService<Bitmap>
            ?? throw new InvalidOperationException(
                $"{nameof(UnitItemControl)} needs a bitmap image service, but {service.GetType().FullName} was registered.");

        // Captured next to the request so the image and the tint always belong together.
        var tint = PlayerTint;
        LoadUnitImage(imageService, unitVm, tint, version).SafeFireAndForget();
    }

    private async Task LoadUnitImage(IImageService<Bitmap> imageService, UnitViewModel unitVm,
        string? tint, int version)
    {
        var image = await imageService.GetImage("units/mechs", unitVm.Model.ToUpperInvariant());
        if (version != _requestVersion) return;
        Apply(image, tint);
    }

    private void Apply(Bitmap? image, string? tint)
    {
        if (UnitImage != null) UnitImage.Source = image;
        if (TintBorder == null) return;

        // The tint is cut to the shape of the image, so it only makes sense while both exist.
        if (image == null || string.IsNullOrEmpty(tint) || !Color.TryParse(tint, out var color))
        {
            TintBorder.OpacityMask = null;
            TintBorder.Background = null;
            return;
        }

        TintBorder.OpacityMask = new ImageBrush { Source = image, Stretch = Stretch.Fill };
        TintBorder.Background = new SolidColorBrush(color);
    }
}
