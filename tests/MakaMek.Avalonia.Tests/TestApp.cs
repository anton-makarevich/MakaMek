using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MakaMek.Avalonia.Tests;

public partial class TestApp : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
