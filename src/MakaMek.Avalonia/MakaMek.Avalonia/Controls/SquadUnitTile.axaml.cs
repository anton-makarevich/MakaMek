using System;
using System.Threading.Tasks;
using System.Windows.Input;
using AsyncAwaitBestPractices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Services;

namespace Sanet.MakaMek.Avalonia.Controls;

/// <summary>
/// One unit in the squad bar: enough to find and identify it, and a click to select it.
/// </summary>
public partial class SquadUnitTile : UserControl
{
    private int _requestVersion;

    public static readonly StyledProperty<ICommand?> SelectCommandProperty =
        AvaloniaProperty.Register<SquadUnitTile, ICommand?>(nameof(SelectCommand));

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<SquadUnitTile, bool>(nameof(IsActive));

    /// <summary>
    /// Image service the tile loads its icon from. The host view binds it from
    /// <c>BattleMapViewModel</c>, so the tile declares its dependency instead of locating one.
    /// </summary>
    public static readonly StyledProperty<IImageService?> ImageServiceProperty =
        AvaloniaProperty.Register<SquadUnitTile, IImageService?>(nameof(ImageService));

    static SquadUnitTile()
    {
        ImageServiceProperty.Changed.AddClassHandler<SquadUnitTile>((tile, _) => tile.LoadUnitImage());
    }

    /// <summary>
    /// Gets or sets the command that selects this tile's unit.
    /// </summary>
    public ICommand? SelectCommand
    {
        get => GetValue(SelectCommandProperty);
        set => SetValue(SelectCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets whether this tile's unit is the one being acted with.
    /// </summary>
    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    /// <summary>
    /// Gets or sets the image service the tile's icon is loaded from.
    /// </summary>
    public IImageService? ImageService
    {
        get => GetValue(ImageServiceProperty);
        set => SetValue(ImageServiceProperty, value);
    }

    public SquadUnitTile()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            ShowFacing();
            LoadUnitImage();
        };
    }

    private void ShowFacing()
    {
        // A unit that has not deployed has no facing and stays upright.
        UnitIcon.RenderTransform = DataContext is IUnit { Facing: { } facing }
            ? new RotateTransform((int)facing * 60)
            : null;
    }

    private void LoadUnitImage()
    {
        // Bumped per request so an image that arrives after the tile changed hands is discarded.
        var version = ++_requestVersion;

        if (DataContext is not IUnit unit || ImageService is not { } service)
        {
            Apply(null, null);
            return;
        }

        var imageService = service as IImageService<Bitmap>
            ?? throw new InvalidOperationException(
                $"{nameof(SquadUnitTile)} needs a bitmap image service, but {service.GetType().FullName} was registered.");

        // Captured next to the request so the image and the tint always belong together.
        var tint = unit.Owner?.Tint;
        FetchImage(imageService, unit, tint, version).SafeFireAndForget();
    }

    private async Task FetchImage(IImageService<Bitmap> imageService, IUnit unit, string? tint, int version)
    {
        var image = await imageService.GetImage("units/mechs", unit.Model.ToUpperInvariant());
        if (version != _requestVersion) return;
        Apply(image, tint);
    }

    private void Apply(Bitmap? image, string? tint)
    {
        if (UnitImage != null) UnitImage.Source = image;
        if (UnitTint == null) return;

        // The tint is cut to the shape of the image, so it only makes sense while both exist.
        if (image == null || string.IsNullOrEmpty(tint) || !Color.TryParse(tint, out var color))
        {
            UnitTint.OpacityMask = null;
            UnitTint.Background = null;
            return;
        }

        UnitTint.OpacityMask = new ImageBrush { Source = image, Stretch = Stretch.Uniform };
        UnitTint.Background = new SolidColorBrush(color);
    }
}
