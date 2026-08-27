using Android.App;
using Android.Content.PM;
using Android.Views;
using Avalonia.Android;

namespace Sanet.MakaMek.Avalonia.Android;

[Activity(
    Label = "MakaMek.Avalonia.Android",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode,
    WindowSoftInputMode = SoftInput.AdjustResize)]
public class MainActivity : AvaloniaMainActivity
{
}
