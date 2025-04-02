using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Sanet.MakaMek.Core.Models.Map;

namespace Sanet.MakaMek.Avalonia.Controls
{
    public partial class DirectionSelector : UserControl
    {
        public DirectionSelector()
        { 
            InitializeComponent();
            IsHitTestVisible = false;
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

        public static readonly DirectProperty<DirectionSelector,HexCoordinates?> PositionProperty =
            AvaloniaProperty.RegisterDirect<DirectionSelector, HexCoordinates?>(nameof(Position),
                o => o.Position,
                (o, v) => o.Position = v);

        private HexCoordinates? _position;
        public HexCoordinates? Position
        {
            get => _position;
            set
            {
                SetAndRaise(PositionProperty, ref _position, value);
                if (value == null) return;
                Canvas.SetLeft(this, value.H-35);
                Canvas.SetTop(this, value.V-38.5);
            }
        }

        public static readonly DirectProperty<DirectionSelector,string> ForegroundProperty =
            AvaloniaProperty.RegisterDirect<DirectionSelector, string>(nameof(Foreground),
                o=> o.Foreground,
                (o, v) => o.Foreground = v);

        private string _foreground; 
        public string Foreground
        {
            get => _foreground;
            set => SetAndRaise(ForegroundProperty, ref _foreground, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property != ForegroundProperty) return;
            if (string.IsNullOrEmpty(Foreground)) return;
       
            var color = Color.Parse(Foreground);
            var brush = new SolidColorBrush(color);
            
            if (TopButton?.Content is Path topPath)
                topPath.Fill = brush;
            if (TopRightButton?.Content is Path topRightPath)
                topRightPath.Fill = brush;
            if (BottomRightButton?.Content is Path bottomRightPath)
                bottomRightPath.Fill = brush;
            if (BottomButton?.Content is Path bottomPath)
                bottomPath.Fill = brush;
            if (BottomLeftButton?.Content is Path bottomLeftPath)
                bottomLeftPath.Fill = brush;
            if (TopLeftButton?.Content is Path topLeftPath)
                topLeftPath.Fill = brush;
        }

        public bool HandleInteraction(Point position)
        {
            if (!IsVisible)
                return false;
            
            var clickedButton = ButtonsContainer.Children
                .OfType<Button>()
                .FirstOrDefault(b => b.Bounds.Contains(position));
            if (clickedButton is not { IsEnabled: true, IsVisible: true }) return false;
            clickedButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            return true;
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
