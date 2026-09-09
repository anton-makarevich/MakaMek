using Avalonia;
using Avalonia.Controls;
using Sanet.MakaMek.Core.Services.Transport;

namespace Sanet.MakaMek.Avalonia.Controls.TemplatedControls;

public class ConnectionStatusBanner : ContentControl
{
    public static readonly StyledProperty<ConnectionStatus> ConnectionStatusProperty =
        AvaloniaProperty.Register<ConnectionStatusBanner, ConnectionStatus>(
            nameof(ConnectionStatus),
            defaultValue: ConnectionStatus.NotConnected);

    public ConnectionStatus ConnectionStatus
    {
        get => GetValue(ConnectionStatusProperty);
        set => SetValue(ConnectionStatusProperty, value);
    }
}
