using System;
using System.Threading.Tasks;
using System.Windows.Input;
using AsyncAwaitBestPractices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Services;

namespace Sanet.MakaMek.Avalonia.Controls;

/// <summary>
/// One unit in the squad bar: enough to find and identify it, and a click to select it.
/// </summary>
public partial class SquadUnitTile : UserControl
{
    private readonly IImageService<Bitmap>? _imageService;
    private readonly ILogger<SquadUnitTile>? _logger;

    public static readonly StyledProperty<ICommand?> SelectCommandProperty =
        AvaloniaProperty.Register<SquadUnitTile, ICommand?>(nameof(SelectCommand));

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<SquadUnitTile, bool>(nameof(IsActive));

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

    public SquadUnitTile()
    {
        InitializeComponent();

        var serviceProvider = ((App?)Application.Current)?.ServiceProvider;
        _imageService = serviceProvider?.GetService<IImageService>() as IImageService<Bitmap>;
        _logger = serviceProvider?.GetService<ILogger<SquadUnitTile>>();
        DataContextChanged += (_, _) => LoadUnitImage().SafeFireAndForget();
    }

    private async Task LoadUnitImage()
    {
        if (_imageService == null || DataContext is not IUnit unit) return;
        try
        {
            var image = await _imageService.GetImage("units/mechs", unit.Model.ToUpperInvariant());
            // The tile may have been handed another unit while the image was loading.
            if (image != null && ReferenceEquals(DataContext, unit))
                UnitImage.Source = image;
        }
        catch (Exception ex)
        {
            // A tile without its image still identifies the unit by pilot and tonnage.
            _logger?.LogError(ex, "Error loading squad tile image for {Model}", unit.Model);
        }
    }
}
