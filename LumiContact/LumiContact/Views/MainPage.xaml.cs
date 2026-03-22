using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using LumiContact.ViewModels;

namespace LumiContact.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class MainPage : ContentPage
    {
        private MainViewModel _viewModel;

        public MainPage()
        {
            InitializeComponent();
            // Applique le thème seulement si on n'a pas encore de ViewModel, 
            // bien que le BindingContext en XAML s'en charge déjà.
            if (BindingContext == null)
            {
                ApplyTheme("light"); 
            }
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            _viewModel = BindingContext as MainViewModel;

            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
                ApplyTheme(_viewModel.Theme);
            }
        }

        private async void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsDetailVisible))
            {
                if (_viewModel.IsDetailVisible)
                {
                    DetailOverlay.IsVisible = true;
                    DetailOverlay.TranslationX = 500;
                    await DetailOverlay.TranslateTo(0, 0, 300, Easing.CubicOut);
                }
                else
                {
                    await DetailOverlay.TranslateTo(500, 0, 250, Easing.CubicIn);
                    DetailOverlay.IsVisible = false;
                }
            }
            else if (e.PropertyName == nameof(MainViewModel.IsAddFormVisible))
            {
                if (_viewModel.IsAddFormVisible)
                {
                    AddOverlay.IsVisible = true;
                    AddOverlay.TranslationY = 800;
                    await AddOverlay.TranslateTo(0, 0, 350, Easing.CubicOut);
                }
                else
                {
                    await AddOverlay.TranslateTo(0, 800, 250, Easing.CubicIn);
                    AddOverlay.IsVisible = false;
                }
            }
            else if (e.PropertyName == nameof(MainViewModel.IsSettingsVisible))
            {
                if (_viewModel.IsSettingsVisible)
                {
                    SettingsOverlay.IsVisible = true;
                    SettingsOverlay.TranslationX = 500;
                    await SettingsOverlay.TranslateTo(0, 0, 300, Easing.CubicOut);
                }
                else
                {
                    await SettingsOverlay.TranslateTo(500, 0, 250, Easing.CubicIn);
                    SettingsOverlay.IsVisible = false;
                }
            }
            else if (e.PropertyName == nameof(MainViewModel.IsToastVisible))
            {
                if (_viewModel.IsToastVisible)
                {
                    ToastView.TranslationY = -20;
                    await Task.WhenAll(
                        ToastView.FadeTo(1, 200),
                        ToastView.TranslateTo(0, 0, 200, Easing.CubicOut)
                    );
                }
                else
                {
                    await Task.WhenAll(
                        ToastView.FadeTo(0, 200),
                        ToastView.TranslateTo(0, -20, 200, Easing.CubicIn)
                    );
                }
            }
            else if (e.PropertyName == nameof(MainViewModel.Theme))
            {
                ApplyTheme(_viewModel.Theme);
            }
        }

        private void ApplyTheme(string theme)
        {
            if (theme == "dark")
            {
                Resources["AppBg"] = Color.FromHex("#050505");
                Resources["ContainerBg"] = Color.FromHex("#0A0A0A");
                Resources["TextMain"] = Color.FromHex("#FFFFFF");
                Resources["TextMuted"] = Color.FromHex("#737373");
                Resources["Border"] = Color.FromHex("#333333");
                Resources["BorderStrong"] = Color.FromHex("#4D4D4D");
                Resources["Accent"] = Color.FromHex("#D4AF37");
                Resources["AccentText"] = Color.FromHex("#000000");
                Resources["InputBg"] = Color.FromHex("#1A1A1A");
                Resources["ToastBg"] = Color.FromHex("#1A1A1A");
                Resources["AvatarBg"] = Color.FromHex("#262626");
                Resources["HeaderGrad"] = Color.FromHex("#111111");

                Resources["TextMainBrush"] = new SolidColorBrush(Color.FromHex("#FFFFFF"));
                Resources["TextMutedBrush"] = new SolidColorBrush(Color.FromHex("#737373"));
                Resources["AccentBrush"] = new SolidColorBrush(Color.FromHex("#D4AF37"));
                Resources["AccentTextBrush"] = new SolidColorBrush(Color.FromHex("#000000"));
            }
            else
            {
                // light
                Resources["AppBg"] = Color.FromHex("#EFECE7");
                Resources["ContainerBg"] = Color.FromHex("#FCFBF8");
                Resources["TextMain"] = Color.FromHex("#1A1A1A");
                Resources["TextMuted"] = Color.FromHex("#8C8C8C");
                Resources["Border"] = Color.FromHex("#EAEAEA");
                Resources["BorderStrong"] = Color.FromHex("#D1D1D1");
                Resources["Accent"] = Color.FromHex("#AA8655");
                Resources["AccentText"] = Color.FromHex("#FFFFFF");
                Resources["InputBg"] = Color.FromHex("#F2F0EB");
                Resources["ToastBg"] = Color.FromHex("#FFFFFF");
                Resources["AvatarBg"] = Color.FromHex("#F2F0EB");
                Resources["HeaderGrad"] = Color.FromHex("#FCFBF8");

                Resources["TextMainBrush"] = new SolidColorBrush(Color.FromHex("#1A1A1A"));
                Resources["TextMutedBrush"] = new SolidColorBrush(Color.FromHex("#8C8C8C"));
                Resources["AccentBrush"] = new SolidColorBrush(Color.FromHex("#AA8655"));
                Resources["AccentTextBrush"] = new SolidColorBrush(Color.FromHex("#FFFFFF"));
            }
        }

        private async void OnLightThemeTapped(object sender, EventArgs e)
        {
            await AnimateSettingsTargetAsync(sender);
            ExecuteCommand(_viewModel?.SetThemeCommand, "light");
        }

        private async void OnDarkThemeTapped(object sender, EventArgs e)
        {
            await AnimateSettingsTargetAsync(sender);
            ExecuteCommand(_viewModel?.SetThemeCommand, "dark");
        }

        private async void OnSyncNowTapped(object sender, EventArgs e)
        {
            await AnimateSettingsTargetAsync(sender);
            ExecuteCommand(_viewModel?.SyncCommand);
        }

        private async void OnImportContactsTapped(object sender, EventArgs e)
        {
            await AnimateSettingsTargetAsync(sender);
            ExecuteCommand(_viewModel?.ImportContactsCommand);
        }

        private static void ExecuteCommand(System.Windows.Input.ICommand command, object parameter = null)
        {
            if (command != null && command.CanExecute(parameter))
            {
                command.Execute(parameter);
            }
        }

        private static VisualElement ResolveAnimationTarget(object sender)
        {
            var element = sender as Element;

            while (element != null)
            {
                if (element is VisualElement visualElement)
                    return visualElement;

                element = element.Parent;
            }

            return null;
        }

        private static async Task AnimateSettingsTargetAsync(object sender)
        {
            var target = ResolveAnimationTarget(sender);
            if (target == null)
                return;

            target.AbortAnimation("SettingsTapScale");
            target.Scale = 1;

            await target.ScaleTo(0.97, 80, Easing.CubicOut);
            await target.ScaleTo(1, 140, Easing.CubicIn);
        }
    }
}
