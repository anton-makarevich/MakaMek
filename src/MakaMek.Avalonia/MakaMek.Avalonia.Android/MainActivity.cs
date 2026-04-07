using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Avalonia.Android;

namespace Sanet.MakaMek.Avalonia.Android;

[Activity(
    Label = "MakaMek.Avalonia.Android",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{ 
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        
        // Make the status bar transparent and ensure content can go behind it
        if (Window != null)
        {
            // Set the status bar to be semi-transparent
            Window.AddFlags(WindowManagerFlags.Fullscreen);
        }
    }
}