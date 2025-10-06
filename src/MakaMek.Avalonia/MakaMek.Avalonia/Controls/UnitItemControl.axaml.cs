using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Sanet.MakaMek.Avalonia.Controls;

public partial class UnitItemControl : UserControl
{
    private readonly IImageService<Bitmap>? _imageService;

    public static readonly StyledProperty<ICommand?> RemoveCommandProperty =
        AvaloniaProperty.Register<UnitItemControl, ICommand?>(nameof(RemoveCommand));

    public static readonly StyledProperty<object?> RemoveCommandParameterProperty =
        AvaloniaProperty.Register<UnitItemControl, object?>(nameof(RemoveCommandParameter));

    public static readonly StyledProperty<bool> IsRemoveButtonVisibleProperty =
        AvaloniaProperty.Register<UnitItemControl, bool>(nameof(IsRemoveButtonVisible), defaultValue: false);

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

    public UnitItemControl()
    {
        InitializeComponent();

        // Try to get image service from App resources
        var serviceProvider = ((App?)Application.Current)?.ServiceProvider;
        if (serviceProvider == null) return;
        var imageService = serviceProvider.GetService<IImageService>();
        _imageService = imageService as IImageService<Bitmap>;
        if (_imageService == null) return;

        DataContextChanged += OnDataContextChanged;
    }
    
    private async void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is UnitData unitData && _imageService != null)
        {
            await LoadUnitImage(unitData);
        }
    }
    
    private async Task LoadUnitImage(UnitData unitData)
    {
        if (_imageService == null) return;
        
        try
        {
            var image = await _imageService.GetImage("units/mechs", unitData.Model.ToUpper());
            if (image != null && UnitImage != null)
            {
                UnitImage.Source = image;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading unit image for {unitData.Model}: {ex.Message}");
        }
    }
}

