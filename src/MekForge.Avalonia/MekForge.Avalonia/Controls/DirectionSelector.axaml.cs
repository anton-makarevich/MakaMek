using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Sanet.MekForge.Core.Models.Map;

namespace Sanet.MekForge.Avalonia.Controls
{
    public partial class DirectionSelector : UserControl
    {
        public DirectionSelector()
        {
            InitializeComponent();
            Width = HexCoordinates.HexWidth*1.65;
            Height = HexCoordinates.HexHeight*1.9;
        }
        
        public static readonly StyledProperty<ICommand?> DirectionSelectedCommandProperty =
            AvaloniaProperty.Register<DirectionSelector, ICommand?>(
                nameof(DirectionSelectedCommand));

        public ICommand? DirectionSelectedCommand
        {
            get => GetValue(DirectionSelectedCommandProperty);
            set => SetValue(DirectionSelectedCommandProperty, value);
        }

        public static readonly DirectProperty<DirectionSelector,IEnumerable<HexDirection>?> EnabledDirectionsProperty =
            AvaloniaProperty.RegisterDirect<DirectionSelector, IEnumerable<HexDirection>?>(
                nameof(EnabledDirections), o => o.EnabledDirections,
                (o, v) => o.EnabledDirections = v);

        private IEnumerable<HexDirection>? _enabledDirections;
        public IEnumerable<HexDirection>? EnabledDirections
        {
            get => _enabledDirections;
            set
            {
                SetAndRaise(EnabledDirectionsProperty, ref _enabledDirections, value);
                TopButton.IsVisible = value?.Contains(HexDirection.Top) ?? false;
                TopRightButton.IsVisible = value?.Contains(HexDirection.TopRight) ?? false;
                BottomRightButton.IsVisible = value?.Contains(HexDirection.BottomRight) ?? false;
                BottomButton.IsVisible = value?.Contains(HexDirection.Bottom) ?? false;
                BottomLeftButton.IsVisible = value?.Contains(HexDirection.BottomLeft) ?? false;
                TopLeftButton.IsVisible = value?.Contains(HexDirection.TopLeft) ?? false;
            }
        }

        public static readonly DirectProperty<DirectionSelector,HexCoordinates> PositionProperty =
            AvaloniaProperty.RegisterDirect<DirectionSelector, HexCoordinates>(
                nameof(Position),
                o => o.Position,
                (o, v) => o.Position = v);

        private HexCoordinates _position;
        public HexCoordinates Position
        {
            get => _position;
            set
            {
                SetAndRaise(PositionProperty, ref _position, value);
                Canvas.SetLeft(this, value.H-35);
                Canvas.SetTop(this, value.V-38.5);
            }
        }

        private void TopButton_Click(object? sender, RoutedEventArgs e)
        {
            DirectionSelectedCommand?.Execute(HexDirection.Top);
        }

        private void TopRightButton_OnClick(object? sender, RoutedEventArgs e)
        {
            DirectionSelectedCommand?.Execute(HexDirection.TopRight);
        }

        private void BottomRightButton_OnClick(object? sender, RoutedEventArgs e)
        {
            DirectionSelectedCommand?.Execute(HexDirection.BottomRight);
        }

        private void BottomButton_OnClick(object? sender, RoutedEventArgs e)
        {
            DirectionSelectedCommand?.Execute(HexDirection.Bottom);
        }

        private void BottomLeftButton_OnClick(object? sender, RoutedEventArgs e)
        {
            DirectionSelectedCommand?.Execute(HexDirection.BottomLeft);
        }

        private void TopLeftButton_OnClick(object? sender, RoutedEventArgs e)
        {
            DirectionSelectedCommand?.Execute(HexDirection.TopLeft);
        }
    }
}
