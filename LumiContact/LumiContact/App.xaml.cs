using System;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using LumiContact.Views;

namespace LumiContact
{
    public partial class App : Application
    {

        public App ()
        {
            Device.SetFlags(new string[] { "Shapes_Experimental" });
            InitializeComponent();

            // DependencyService.Register<MockDataStore>(); // Not needed for the new app
            MainPage = new MainPage();
        }

        protected override void OnStart ()
        {
        }

        protected override void OnSleep ()
        {
        }

        protected override void OnResume ()
        {
        }
    }
}