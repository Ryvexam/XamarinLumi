using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Xamarin.Forms;
using SQLite;
using System.Threading.Tasks;
using LumiContact.Services;
using Xamarin.Essentials;
using System.Collections.Generic;

namespace LumiContact.ViewModels
{
    // Modèle simple
    public class Contact : INotifyPropertyChanged
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        
        private string _firstName;
        public string FirstName { get => _firstName; set { _firstName = value; OnPropertyChanged(); OnPropertyChanged(nameof(FullName)); OnPropertyChanged(nameof(Initial)); } }
        
        private string _lastName;
        public string LastName { get => _lastName; set { _lastName = value; OnPropertyChanged(); OnPropertyChanged(nameof(FullName)); } }
        
        private string _phone;
        public string Phone { get => _phone; set { _phone = value; OnPropertyChanged(); } }
        
        private string _email;
        public string Email { get => _email; set { _email = value; OnPropertyChanged(); } }
        
        private string _comment;
        public string Comment { get => _comment; set { _comment = value; OnPropertyChanged(); } }
        
        private string _photoPath;
        public string PhotoPath { get => _photoPath; set { _photoPath = value; OnPropertyChanged(); } }
        
        // LumiContact Aesthetic fields
        public string Color { get; set; }
        public string CornerString { get; set; }

        [SQLite.Ignore]
        public string FullName => $"{FirstName} {LastName}".Trim();

        [SQLite.Ignore]
        public string Initial
        {
            get
            {
                var f = string.IsNullOrEmpty(FirstName) ? "" : FirstName.Substring(0, 1).ToUpper();
                var l = string.IsNullOrEmpty(LastName) ? "" : LastName.Substring(0, 1).ToUpper();
                var initials = $"{f}{l}";
                return string.IsNullOrEmpty(initials) ? "?" : initials;
            }
        }
        
        [SQLite.Ignore]
        public CornerRadius AvatarCorner 
        { 
            get 
            {
                if (string.IsNullOrEmpty(CornerString))
                    return new CornerRadius(30, 30, 30, 30);
                
                var parts = CornerString.Split(',');
                if (parts.Length == 4)
                    return new CornerRadius(
                        double.Parse(parts[0]), 
                        double.Parse(parts[1]), 
                        double.Parse(parts[2]), 
                        double.Parse(parts[3]));
                
                return new CornerRadius(30, 30, 30, 30);
            }
            set
            {
                CornerString = $"{value.TopLeft},{value.TopRight},{value.BottomLeft},{value.BottomRight}";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class ContactGroup : ObservableCollection<Contact>
    {
        public string Name { get; private set; }

        public ContactGroup(string name, IEnumerable<Contact> contacts) : base(contacts)
        {
            Name = name;
        }
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<Contact> AllContacts { get; set; }
        public ObservableCollection<ContactGroup> GroupedContacts { get; set; }

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                FilterContacts();
                OnPropertyChanged();
            }
        }

        private Contact _selectedContact;
        public Contact SelectedContact
        {
            get => _selectedContact;
            set 
            { 
                if (_selectedContact != value)
                {
                    _selectedContact = value; 
                    OnPropertyChanged(); 
                    if (value != null)
                    {
                        IsDetailVisible = true;
                    }
                }
            }
        }

        // View states
        private bool _isDetailVisible;
        public bool IsDetailVisible
        {
            get => _isDetailVisible;
            set { _isDetailVisible = value; OnPropertyChanged(); }
        }

        private bool _isAddFormVisible;
        public bool IsAddFormVisible
        {
            get => _isAddFormVisible;
            set { _isAddFormVisible = value; OnPropertyChanged(); }
        }

        private bool _isSettingsVisible;
        public bool IsSettingsVisible
        {
            get => _isSettingsVisible;
            set { _isSettingsVisible = value; OnPropertyChanged(); }
        }

        // Settings
        private string _theme = "light";
        public string Theme
        {
            get => _theme;
            set { _theme = value; OnPropertyChanged(); }
        }

        private string _cloudType = "lumiContact";
        public string CloudType
        {
            get => _cloudType;
            set { _cloudType = value; OnPropertyChanged(); }
        }

        private bool _isSyncing;
        public bool IsSyncing
        {
            get => _isSyncing;
            set { _isSyncing = value; OnPropertyChanged(); }
        }

        private string _toastMessage;
        public string ToastMessage
        {
            get => _toastMessage;
            set { _toastMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsToastVisible)); }
        }

        public bool IsToastVisible => !string.IsNullOrEmpty(ToastMessage);

        // New Contact Form Data
        private int _editingId = 0; // 0 for new, ID for editing
        
        private string _newFirstName;
        public string NewFirstName { get => _newFirstName; set { _newFirstName = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsFormValid)); } }
        
        private string _newLastName;
        public string NewLastName { get => _newLastName; set { _newLastName = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsFormValid)); } }
        
        private string _newPhone;
        public string NewPhone { get => _newPhone; set { _newPhone = value; OnPropertyChanged(); } }
        
        private string _newEmail;
        public string NewEmail { get => _newEmail; set { _newEmail = value; OnPropertyChanged(); } }
        
        private string _newComment;
        public string NewComment { get => _newComment; set { _newComment = value; OnPropertyChanged(); } }
        
        private string _newPhotoPath;
        public string NewPhotoPath { get => _newPhotoPath; set { _newPhotoPath = value; OnPropertyChanged(); } }

        public bool IsFormValid => !string.IsNullOrWhiteSpace(NewFirstName) || !string.IsNullOrWhiteSpace(NewLastName);

        public ICommand OpenAddCommand { get; }
        public ICommand CloseAddCommand { get; }
        public ICommand SaveContactCommand { get; }
        public ICommand DeleteContactCommand { get; }
        public ICommand CloseDetailCommand { get; }
        public ICommand SelectContactCommand { get; }
        public ICommand EditContactCommand { get; }
        public ICommand PickPhotoCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand CloseSettingsCommand { get; }
        public ICommand SetThemeCommand { get; }
        public ICommand SetCloudTypeCommand { get; }
        public ICommand SyncCommand { get; }
        public ICommand PhoneCommand { get; }
        public ICommand EmailCommand { get; }

        private bool _isPickingPhoto;

        public MainViewModel()
        {
            AllContacts = new ObservableCollection<Contact>();
            GroupedContacts = new ObservableCollection<ContactGroup>();

            // Load settings
            if (Preferences.ContainsKey("theme")) Theme = Preferences.Get("theme", "light");
            if (Preferences.ContainsKey("cloudType")) CloudType = Preferences.Get("cloudType", "lumiContact");

            OpenAddCommand = new Command(() => 
            {
                ResetForm();
                SelectedContact = null;
                IsAddFormVisible = true;
            });
            
            CloseAddCommand = new Command(() => 
            {
                IsAddFormVisible = false;
                SelectedContact = null;
            });
            CloseDetailCommand = new Command(() => 
            {
                IsDetailVisible = false;
                // Don't set SelectedContact to null immediately to allow the animation to finish
                Device.StartTimer(TimeSpan.FromMilliseconds(300), () =>
                {
                    SelectedContact = null;
                    return false;
                });
            });
            
            OpenSettingsCommand = new Command(() => IsSettingsVisible = true);
            CloseSettingsCommand = new Command(() => IsSettingsVisible = false);

            SetThemeCommand = new Command<string>((t) => 
            {
                Theme = t;
                Preferences.Set("theme", t);
            });

            SetCloudTypeCommand = new Command<string>((c) => 
            {
                CloudType = c;
                Preferences.Set("cloudType", c);
            });

            SyncCommand = new Command(() => 
            {
                ShowToast("Pas encore implémenté");
            });

            SelectContactCommand = new Command(() => {
                // Handled in setter
            });

            DeleteContactCommand = new Command<Contact>(async (c) => 
            {
                if (c == null) return;
                
                // Usually we'd want a dialog here, but viewmodels shouldn't call DisplayAlert directly.
                // Assuming it's deleted straight away or user confirms in UI.
                await DatabaseService.DeleteContactAsync(c);

                AllContacts.Remove(c);
                FilterContacts();
                SelectedContact = null;
                IsDetailVisible = false;
                ShowToast("Contact supprimé");
            });

            EditContactCommand = new Command(() =>
            {
                if (SelectedContact == null) return;
                
                _editingId = SelectedContact.Id;
                NewFirstName = SelectedContact.FirstName;
                NewLastName = SelectedContact.LastName;
                NewPhone = SelectedContact.Phone;
                NewEmail = SelectedContact.Email;
                NewComment = SelectedContact.Comment;
                NewPhotoPath = SelectedContact.PhotoPath;

                IsDetailVisible = false;
                IsAddFormVisible = true;
            });
            
            PickPhotoCommand = new Command(async () =>
            {
                if (_isPickingPhoto) return;
                _isPickingPhoto = true;

                try
                {
                    // Let Xamarin.Essentials handle permissions automatically. 
                    // It was updated in 1.7.7 to support Android 13 READ_MEDIA_IMAGES.
                    var result = await MediaPicker.PickPhotoAsync(new MediaPickerOptions { Title = "Sélectionner une photo" });

                    if (result != null)
                    {
                        var stream = await result.OpenReadAsync();
                        byte[] imageData;
                        using (var memoryStream = new System.IO.MemoryStream())
                        {
                            await stream.CopyToAsync(memoryStream);
                            imageData = memoryStream.ToArray();
                        }

                        // Resize image to max 500x500 to prevent OOM
                        var resizer = DependencyService.Get<IImageResizer>();
                        if (resizer != null)
                        {
                            imageData = await resizer.ResizeImageAsync(imageData, 500, 500);
                        }

                        var newFile = System.IO.Path.Combine(FileSystem.AppDataDirectory, result.FileName);
                        System.IO.File.WriteAllBytes(newFile, imageData);
                        NewPhotoPath = newFile;
                    }
                }
                catch (FeatureNotSupportedException fnsEx)
                {
                    ShowToast("Appareil non supporté");
                    Console.WriteLine(fnsEx.Message);
                }
                catch (PermissionException pEx)
                {
                    ShowToast("Permission photo refusée");
                    Console.WriteLine(pEx.Message);
                }
                catch (Exception ex)
                {
                    ShowToast("Erreur photo : " + ex.Message);
                    Console.WriteLine(ex.Message);
                }
                finally
                {
                    _isPickingPhoto = false;
                }
            });

            SaveContactCommand = new Command(async () =>
            {
                if (!IsFormValid) return;
                
                if (_editingId == 0)
                {
                    var newC = new Contact
                    {
                        FirstName = NewFirstName,
                        LastName = NewLastName,
                        Phone = NewPhone,
                        Email = NewEmail,
                        Comment = NewComment,
                        PhotoPath = NewPhotoPath,
                    };
                    
                    await DatabaseService.SaveContactAsync(newC);
                    AllContacts.Add(newC);
                    ShowToast("Contact ajouté avec succès");
                }
                else
                {
                    var existing = AllContacts.FirstOrDefault(x => x.Id == _editingId);
                    if (existing != null)
                    {
                        existing.FirstName = NewFirstName;
                        existing.LastName = NewLastName;
                        existing.Phone = NewPhone;
                        existing.Email = NewEmail;
                        existing.Comment = NewComment;
                        existing.PhotoPath = NewPhotoPath;
                        
                        await DatabaseService.SaveContactAsync(existing);
                        ShowToast("Contact mis à jour");
                    }
                }

                FilterContacts();
                IsAddFormVisible = false;
                SelectedContact = null;
                ResetForm();
            });

            PhoneCommand = new Command(() => {
                if(SelectedContact != null && !string.IsNullOrEmpty(SelectedContact.Phone))
                    Launcher.OpenAsync(new Uri($"tel:{SelectedContact.Phone}"));
            });

            EmailCommand = new Command(() => {
                if(SelectedContact != null && !string.IsNullOrEmpty(SelectedContact.Email))
                    Launcher.OpenAsync(new Uri($"mailto:{SelectedContact.Email}"));
            });

            Task.Run(async () => await LoadData());
        }

        private async void ShowToast(string message)
        {
            ToastMessage = message;
            await Task.Delay(3000);
            if (ToastMessage == message) // In case it was overridden
                ToastMessage = "";
        }

        void ResetForm()
        {
            _editingId = 0;
            NewFirstName = ""; 
            NewLastName = ""; 
            NewPhone = ""; 
            NewEmail = "";
            NewComment = "";
            NewPhotoPath = null;
        }

        async Task LoadData()
        {
            var list = await DatabaseService.GetContactsAsync();
            Device.BeginInvokeOnMainThread(() =>
            {
                AllContacts.Clear();
                foreach (var item in list)
                {
                    AllContacts.Add(item);
                }
                FilterContacts();
            });
        }

        void FilterContacts()
        {
            var query = SearchText?.ToLower() ?? "";
            
            var filtered = AllContacts
                .Where(x => (x.FirstName + " " + x.LastName).ToLower().Contains(query) || (x.Phone ?? "").Contains(query))
                .OrderBy(x => x.LastName ?? "")
                .ThenBy(x => x.FirstName ?? "")
                .ToList();
            
            var groups = filtered.GroupBy(c => {
                var name = string.IsNullOrEmpty(c.LastName) ? c.FirstName : c.LastName;
                var letter = string.IsNullOrEmpty(name) ? "#" : name.Substring(0, 1).ToUpper();
                if (!char.IsLetter(letter[0])) letter = "#";
                return letter;
            }).OrderBy(g => g.Key).ToList();

            GroupedContacts.Clear();
            foreach (var group in groups)
            {
                GroupedContacts.Add(new ContactGroup(group.Key, group));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}