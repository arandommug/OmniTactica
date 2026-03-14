using Android.App;
using Android.Content.PM;
using Android.OS;

namespace OmniTactica
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, HardwareAccelerated = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            ApplyPreferredRefreshRate();
        }

        protected override void OnResume()
        {
            base.OnResume();
            ApplyPreferredRefreshRate();
        }

        private void ApplyPreferredRefreshRate()
        {
            var preferredRefreshRate = WindowManager?.DefaultDisplay?.RefreshRate ?? 0f;

            if (preferredRefreshRate <= 0f || Window?.Attributes is not Android.Views.WindowManagerLayoutParams attributes)
            {
                return;
            }

            if (Math.Abs(attributes.PreferredRefreshRate - preferredRefreshRate) < 0.1f)
            {
                return;
            }

            attributes.PreferredRefreshRate = preferredRefreshRate;
            Window.Attributes = attributes;
        }
    }
}
