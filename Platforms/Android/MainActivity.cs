using Android.App;
using Android.Content.PM;
using Android.OS;

namespace SpeakSharp;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnDestroy()
    {
        // Clear native focus before MAUI disposes its service provider. Some
        // Android builds otherwise dispatch Entry.OnFocusChange after disposal.
        CurrentFocus?.ClearFocus();
        base.OnDestroy();
    }
}
