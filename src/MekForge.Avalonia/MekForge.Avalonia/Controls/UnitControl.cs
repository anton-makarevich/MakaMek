using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Sanet.MekForge.Core.Models;
using Sanet.MekForge.Core.Models.Units;
using Sanet.MekForge.Core.Services;
using System.Reactive.Linq;

namespace Sanet.MekForge.Avalonia.Controls
{
    public class UnitControl : Grid, IDisposable
    {
        private readonly Image _unitImage;
        private readonly IImageService<Bitmap> _imageService;
        private readonly Unit _unit;
        private readonly IDisposable _subscription;

        public UnitControl(Unit unit, IImageService<Bitmap> imageService)
        {
            _unit = unit;
            _imageService = imageService;
            
            Width = HexCoordinates.HexWidth;
            Height = HexCoordinates.HexHeight;
            
            _unitImage = new Image
            {
                Width = Width * 0.84,
                Height = Height * 0.84,
                Stretch = Stretch.Fill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Children.Add(_unitImage);

            // Create an observable that polls the unit's position
            var positionChanges = Observable
                .Interval(TimeSpan.FromMilliseconds(16)) // ~60fps
                .Select(_ => _unit.Position)
                .DistinctUntilChanged();

            _subscription = positionChanges.Subscribe(_ => UpdatePosition());
            
            // Initial update
            UpdatePosition();
            UpdateImage();
        }

        private void UpdatePosition()
        {
            if (_unit.Position == null) return;
            var hexPosition = _unit.Position;
            SetValue(Canvas.LeftProperty, hexPosition.Value.X);
            SetValue(Canvas.TopProperty, _unit.Position.Value.Y);            
        }
        
        private void UpdateImage()
        {
            var image = _imageService.GetImage("units/mechs", _unit.Model.ToUpper());
            if (image != null)
            {
                _unitImage.Source = image;
            }
        }

        public void Dispose()
        {
            _subscription.Dispose();
        }
    }
}
