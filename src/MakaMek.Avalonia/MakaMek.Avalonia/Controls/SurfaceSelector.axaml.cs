using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Avalonia.Controls
{
    public partial class SurfaceSelector : UserControl
    {
        public SurfaceSelector()
        {
            InitializeComponent();
            IsHitTestVisible = false;
        }

        public static readonly StyledProperty<ICommand?> SurfaceSelectedCommandProperty =
            AvaloniaProperty.Register<SurfaceSelector, ICommand?>(
                nameof(SurfaceSelectedCommand));

        public ICommand? SurfaceSelectedCommand
        {
            get => GetValue(SurfaceSelectedCommandProperty);
            set => SetValue(SurfaceSelectedCommandProperty, value);
        }

        public static readonly DirectProperty<SurfaceSelector, IEnumerable<HexReachabilityViewModel>?> OptionsProperty =
            AvaloniaProperty.RegisterDirect<SurfaceSelector, IEnumerable<HexReachabilityViewModel>?>(
                nameof(Options), o => o.Options,
                (o, v) => o.Options = v);

        private IEnumerable<HexReachabilityViewModel>? _options;
        public IEnumerable<HexReachabilityViewModel>? Options
        {
            get => _options;
            set
            {
                SetAndRaise(OptionsProperty, ref _options, value);
                UpdateButtonVisibility();
            }
        }

        public static readonly DirectProperty<SurfaceSelector, HexCoordinates?> PositionProperty =
            AvaloniaProperty.RegisterDirect<SurfaceSelector, HexCoordinates?>(nameof(Position),
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
                Canvas.SetLeft(this, value.H - 35);
                Canvas.SetTop(this, value.V - 38.5);
            }
        }

        private void UpdateButtonVisibility()
        {
            if (_options == null)
            {
                GroundButton.IsVisible = false;
                BridgeButton.IsVisible = false;
                return;
            }

            var optionsList = _options.ToList();
            var groundOption = optionsList.FirstOrDefault(o => o.Surface == HexSurface.Ground);
            GroundButton.IsVisible = groundOption != null;
            GroundButton.Content = groundOption?.FormattedLabel;

            var bridgeOption = optionsList.FirstOrDefault(o => o.Surface == HexSurface.Bridge);
            BridgeButton.IsVisible = bridgeOption != null;
            BridgeButton.Content = bridgeOption?.FormattedLabel;
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

        private void GroundButton_OnClick(object? sender, RoutedEventArgs e)
        {
            SurfaceSelectedCommand?.Execute(HexSurface.Ground);
        }

        private void BridgeButton_OnClick(object? sender, RoutedEventArgs e)
        {
            SurfaceSelectedCommand?.Execute(HexSurface.Bridge);
        }
    }
}
