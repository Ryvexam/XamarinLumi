using Android.App;
using Android.Content;
using Android.OS;

namespace LumiContact.Droid
{
    [Activity(Theme = "@style/MyTheme.Splash", MainLauncher = true, NoHistory = true, Exported = true)]
    public class SplashActivity : Activity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
        }

        protected override void OnResume()
        {
            base.OnResume();
            
            // Launch the main activity directly and securely
            var intent = new Intent(this, typeof(MainActivity));
            StartActivity(intent);
            
            // Critical: Finish the SplashActivity so we don't get stuck in a loop or memory
            Finish();
        }
    }
}