using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Services;

namespace Sanet.MakaMek.Avalonia.Controls;

public partial class UnitItemControl : UserControl
{
    private readonly IImageService<Bitmap>? _imageService;

    public UnitItemControl()
    {
        InitializeComponent();

        // Try to get image service from App resources
        if (Application.Current?.Resources.TryGetResource("ImageService", null, out var service) == true)
        {
            _imageService = service as IImageService<Bitmap>;
        }

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

