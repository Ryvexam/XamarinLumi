using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using LumiContact.Services;
using SQLite;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace LumiContact.ViewModels
{
    public class Contact : INotifyPropertyChanged
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        private string _firstName;
        public string FirstName
        {
            get => _firstName;
            set
            {
                _firstName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FullName));
                OnPropertyChanged(nameof(Initial));
            }
        }

        private string _lastName;
        public string LastName
        {
            get => _lastName;
            set
            {
                _lastName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FullName));
                OnPropertyChanged(nameof(Initial));
            }
        }

        private string _phone;
        public string Phone
        {
            get => _phone;
            set
            {
                _phone = value;
                OnPropertyChanged();
            }
        }

        private string _email;
        public string Email
        {
            get => _email;
            set
            {
                _email = value;
                OnPropertyChanged();
            }
        }

        private string _comment;
        public string Comment
        {
            get => _comment;
            set
            {
                _comment = value;
                OnPropertyChanged();
            }
        }

        private string _photoPath;
        public string PhotoPath
        {
            get => _photoPath;
            set
            {
                _photoPath = value;
                OnPropertyChanged();
            }
        }

        private bool _isFavorite;
        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                _isFavorite = value;
                OnPropertyChanged();
            }
        }

        private string _remoteId;
        public string RemoteId
        {
            get => _remoteId;
            set
            {
                _remoteId = value;
                OnPropertyChanged();
            }
        }

        public long RemoteVersion { get; set; }

        public bool NeedsSync { get; set; }

        public string LastSyncedAtUtc { get; set; }

        public string Color { get; set; }

        public string CornerString { get; set; }

        [SQLite.Ignore]
        public string FullName => $"{FirstName} {LastName}".Trim();

        [SQLite.Ignore]
        public string Initial
        {
            get
            {
                var first = string.IsNullOrEmpty(FirstName) ? string.Empty : FirstName.Substring(0, 1).ToUpper();
                var last = string.IsNullOrEmpty(LastName) ? string.Empty : LastName.Substring(0, 1).ToUpper();
                var initials = $"{first}{last}";
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
                {
                    return new CornerRadius(
                        double.Parse(parts[0]),
                        double.Parse(parts[1]),
                        double.Parse(parts[2]),
                        double.Parse(parts[3]));
                }

                return new CornerRadius(30, 30, 30, 30);
            }
            set
            {
                CornerString = $"{value.TopLeft},{value.TopRight},{value.BottomLeft},{value.BottomRight}";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
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
        private readonly ContactSyncService _syncService;
        private bool _isPickingPhoto;
        private bool _isApplyingRemoteChanges;
        private CancellationTokenSource _serverCheckCancellation;

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
                if (_selectedContact == value)
                    return;

                _selectedContact = value;
                OnPropertyChanged();

                if (value != null)
                {
                    IsDetailVisible = true;
                }
            }
        }

        private bool _isDetailVisible;
        public bool IsDetailVisible
        {
            get => _isDetailVisible;
            set
            {
                _isDetailVisible = value;
                OnPropertyChanged();
            }
        }

        private bool _isAddFormVisible;
        public bool IsAddFormVisible
        {
            get => _isAddFormVisible;
            set
            {
                _isAddFormVisible = value;
                OnPropertyChanged();
            }
        }

        private bool _isSettingsVisible;
        public bool IsSettingsVisible
        {
            get => _isSettingsVisible;
            set
            {
                _isSettingsVisible = value;
                OnPropertyChanged();
            }
        }

        private string _theme = "light";
        public string Theme
        {
            get => _theme;
            set
            {
                _theme = value;
                OnPropertyChanged();
            }
        }

        private string _serverUrl = string.Empty;
        public string ServerUrl
        {
            get => _serverUrl;
            set
            {
                var normalized = string.IsNullOrWhiteSpace(value)
                    ? string.Empty
                    : value.Trim();
                _serverUrl = normalized;
                Preferences.Set("serverUrl", normalized);
                OnPropertyChanged();
                ScheduleServerConnectionCheck();
            }
        }

        private bool _isCheckingServerConnection;
        public bool IsCheckingServerConnection
        {
            get => _isCheckingServerConnection;
            set
            {
                _isCheckingServerConnection = value;
                OnPropertyChanged();
            }
        }

        private bool _isServerConnected;
        public bool IsServerConnected
        {
            get => _isServerConnected;
            set
            {
                _isServerConnected = value;
                OnPropertyChanged();
            }
        }

        private string _serverConnectionStatus = "Aucune URL serveur";
        public string ServerConnectionStatus
        {
            get => _serverConnectionStatus;
            set
            {
                _serverConnectionStatus = value;
                OnPropertyChanged();
            }
        }

        private bool _isSyncing;
        public bool IsSyncing
        {
            get => _isSyncing;
            set
            {
                _isSyncing = value;
                OnPropertyChanged();
            }
        }

        private string _toastMessage;
        public string ToastMessage
        {
            get => _toastMessage;
            set
            {
                _toastMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsToastVisible));
            }
        }

        public bool IsToastVisible => !string.IsNullOrEmpty(ToastMessage);

        private int _editingId;

        private string _newFirstName;
        public string NewFirstName
        {
            get => _newFirstName;
            set
            {
                _newFirstName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsFormValid));
            }
        }

        private string _newLastName;
        public string NewLastName
        {
            get => _newLastName;
            set
            {
                _newLastName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsFormValid));
            }
        }

        private string _newPhone;
        public string NewPhone
        {
            get => _newPhone;
            set
            {
                _newPhone = value;
                OnPropertyChanged();
            }
        }

        private string _newEmail;
        public string NewEmail
        {
            get => _newEmail;
            set
            {
                _newEmail = value;
                OnPropertyChanged();
            }
        }

        private string _newComment;
        public string NewComment
        {
            get => _newComment;
            set
            {
                _newComment = value;
                OnPropertyChanged();
            }
        }

        private string _newPhotoPath;
        public string NewPhotoPath
        {
            get => _newPhotoPath;
            set
            {
                _newPhotoPath = value;
                OnPropertyChanged();
            }
        }

        public bool IsFormValid =>
            !string.IsNullOrWhiteSpace(NewFirstName) || !string.IsNullOrWhiteSpace(NewLastName);

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

        public ICommand SyncCommand { get; }

        public ICommand PhoneCommand { get; }

        public ICommand EmailCommand { get; }

        public ICommand ToggleFavoriteCommand { get; }

        public ICommand CallContactCommand { get; }

        public ICommand ImportContactsCommand { get; }

        public MainViewModel()
        {
            _syncService = ContactSyncService.Instance;
            _syncService.RemoteContactsChanged += OnRemoteContactsChanged;

            AllContacts = new ObservableCollection<Contact>();
            GroupedContacts = new ObservableCollection<ContactGroup>();

            if (Preferences.ContainsKey("theme"))
                Theme = Preferences.Get("theme", "light");

            ServerUrl = Preferences.Get("serverUrl", string.Empty);

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
                Device.StartTimer(TimeSpan.FromMilliseconds(300), () =>
                {
                    SelectedContact = null;
                    return false;
                });
            });

            OpenSettingsCommand = new Command(() => IsSettingsVisible = true);
            CloseSettingsCommand = new Command(() => IsSettingsVisible = false);

            SetThemeCommand = new Command<string>(theme =>
            {
                Theme = theme;
                Preferences.Set("theme", theme);
            });

            SyncCommand = new Command(async () =>
            {
                await SyncNowAsync();
            });

            SelectContactCommand = new Command<Contact>(contact =>
            {
                if (contact != null)
                    SelectedContact = contact;
            });

            DeleteContactCommand = new Command<Contact>(async contact =>
            {
                await DeleteContactAsync(contact);
            });

            EditContactCommand = new Command(() =>
            {
                if (SelectedContact == null)
                    return;

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
                if (_isPickingPhoto)
                    return;

                _isPickingPhoto = true;

                try
                {
                    var result = await MediaPicker.PickPhotoAsync(
                        new MediaPickerOptions { Title = "Sélectionner une photo" });

                    if (result != null)
                    {
                        var stream = await result.OpenReadAsync();
                        byte[] imageData;

                        using (var memoryStream = new System.IO.MemoryStream())
                        {
                            await stream.CopyToAsync(memoryStream);
                            imageData = memoryStream.ToArray();
                        }

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
                catch (FeatureNotSupportedException featureNotSupportedException)
                {
                    ShowToast("Appareil non supporté");
                    Console.WriteLine(featureNotSupportedException.Message);
                }
                catch (PermissionException permissionException)
                {
                    ShowToast("Permission photo refusée");
                    Console.WriteLine(permissionException.Message);
                }
                catch (Exception exception)
                {
                    ShowToast("Erreur photo : " + exception.Message);
                    Console.WriteLine(exception.Message);
                }
                finally
                {
                    _isPickingPhoto = false;
                }
            });

            SaveContactCommand = new Command(async () =>
            {
                await SaveContactAsync();
            });

            PhoneCommand = new Command(() =>
            {
                if (SelectedContact != null && !string.IsNullOrEmpty(SelectedContact.Phone))
                    Launcher.OpenAsync(new Uri($"tel:{SelectedContact.Phone}"));
            });

            EmailCommand = new Command(() =>
            {
                if (SelectedContact != null && !string.IsNullOrEmpty(SelectedContact.Email))
                    Launcher.OpenAsync(new Uri($"mailto:{SelectedContact.Email}"));
            });

            ToggleFavoriteCommand = new Command<Contact>(async contact =>
            {
                await ToggleFavoriteAsync(contact);
            });

            CallContactCommand = new Command<Contact>(contact =>
            {
                if (contact != null && !string.IsNullOrEmpty(contact.Phone))
                    Launcher.OpenAsync(new Uri($"tel:{contact.Phone}"));
            });

            ImportContactsCommand = new Command(async () =>
            {
                await ImportContactsAsync();
            });

            Task.Run(async () => await InitializeAsync());
        }

        private async Task InitializeAsync()
        {
            await LoadData();

            if (string.IsNullOrWhiteSpace(ServerUrl))
                return;

            try
            {
                await _syncService.ConfigureAsync(ServerUrl);
                ServerConnectionStatus = "Connexion active";
                IsServerConnected = true;
                await _syncService.EnsureRealtimeAsync(ServerUrl);
                await SyncNowAsync(false);
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
                ServerConnectionStatus = "Serveur non joignable";
                IsServerConnected = false;
                ShowToast("Serveur de sync indisponible");
            }
        }

        private async Task SaveContactAsync()
        {
            if (!IsFormValid)
                return;

            Contact workingContact;

            if (_editingId == 0)
            {
                var exists = AllContacts.Any(contact =>
                    contact.Id != _editingId
                    && string.Equals((contact.FirstName ?? string.Empty).Trim(), (NewFirstName ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase)
                    && string.Equals((contact.LastName ?? string.Empty).Trim(), (NewLastName ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));

                if (exists)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Doublon",
                        "Un contact avec ce nom et prénom existe déjà.",
                        "OK");
                    return;
                }

                workingContact = new Contact
                {
                    FirstName = NewFirstName,
                    LastName = NewLastName,
                    Phone = NewPhone,
                    Email = NewEmail,
                    Comment = NewComment,
                    PhotoPath = NewPhotoPath,
                    NeedsSync = true
                };

                await DatabaseService.SaveContactAsync(workingContact);
                AllContacts.Add(workingContact);
                ShowToast("Contact ajouté avec succès");
            }
            else
            {
                workingContact = AllContacts.FirstOrDefault(contact => contact.Id == _editingId);
                if (workingContact == null)
                    return;

                workingContact.FirstName = NewFirstName;
                workingContact.LastName = NewLastName;
                workingContact.Phone = NewPhone;
                workingContact.Email = NewEmail;
                workingContact.Comment = NewComment;
                workingContact.PhotoPath = NewPhotoPath;
                workingContact.NeedsSync = true;

                await DatabaseService.SaveContactAsync(workingContact);
                ShowToast("Contact mis à jour");
            }

            await SyncContactAsync(workingContact, false);

            FilterContacts();
            IsAddFormVisible = false;
            SelectedContact = null;
            ResetForm();
        }

        private async Task DeleteContactAsync(Contact contact)
        {
            if (contact == null)
                return;

            if (!string.IsNullOrWhiteSpace(contact.RemoteId))
            {
                try
                {
                    await _syncService.ConfigureAsync(ServerUrl);
                    await _syncService.DeleteContactAsync(contact);
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception.Message);
                    ShowToast("Suppression distante impossible");
                    return;
                }
            }

            await DatabaseService.DeleteContactAsync(contact);

            AllContacts.Remove(contact);
            FilterContacts();
            SelectedContact = null;
            IsDetailVisible = false;
            ShowToast("Contact supprimé");
        }

        private async Task ToggleFavoriteAsync(Contact contact)
        {
            if (contact == null)
                return;

            contact.IsFavorite = !contact.IsFavorite;
            contact.NeedsSync = true;

            await DatabaseService.SaveContactAsync(contact);
            await SyncContactAsync(contact, false);

            FilterContacts();
            ShowToast(contact.IsFavorite ? "Ajouté aux favoris" : "Retiré des favoris");
        }

        private async Task ImportContactsAsync()
        {
            var confirm = await Application.Current.MainPage.DisplayAlert(
                "Importation",
                "Voulez-vous importer les contacts de votre téléphone ? Les doublons seront ignorés.",
                "Oui, importer",
                "Annuler");

            if (!confirm)
                return;

            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.ContactsRead>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.ContactsRead>();
                }

                if (status != PermissionStatus.Granted)
                {
                    ShowToast("Permission Contacts refusée.");
                    return;
                }

                ShowToast("Importation en cours...");
                var contacts = await Xamarin.Essentials.Contacts.GetAllAsync();
                var count = 0;

                var random = new Random();
                var colors = new[] { "#ab6bbd", "#3498db", "#e74c3c", "#f1c40f", "#2ecc71", "#e67e22" };

                foreach (var nativeContact in contacts)
                {
                    var firstName = nativeContact.GivenName ?? string.Empty;
                    var lastName = nativeContact.FamilyName ?? string.Empty;

                    if (string.IsNullOrEmpty(firstName) && string.IsNullOrEmpty(lastName))
                        continue;

                    var exists = AllContacts.Any(contact =>
                        string.Equals(contact.FirstName ?? string.Empty, firstName, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(contact.LastName ?? string.Empty, lastName, StringComparison.OrdinalIgnoreCase));

                    if (exists)
                        continue;

                    string photoPath = null;
                    var photoService = DependencyService.Get<IContactPhotoService>();
                    if (photoService != null && !string.IsNullOrEmpty(nativeContact.Id))
                    {
                        photoPath = photoService.GetContactPhoto(nativeContact.Id);
                    }

                    var importedContact = new Contact
                    {
                        FirstName = firstName,
                        LastName = lastName,
                        Phone = nativeContact.Phones?.FirstOrDefault()?.PhoneNumber ?? string.Empty,
                        Email = nativeContact.Emails?.FirstOrDefault()?.EmailAddress ?? string.Empty,
                        PhotoPath = photoPath,
                        Color = colors[random.Next(colors.Length)],
                        CornerString = "24",
                        NeedsSync = true
                    };

                    await DatabaseService.SaveContactAsync(importedContact);
                    AllContacts.Add(importedContact);
                    count++;
                }

                await LoadData();

                if (!string.IsNullOrWhiteSpace(ServerUrl))
                {
                    await SyncNowAsync(false);
                }

                IsSettingsVisible = false;

                ShowToast(count > 0
                    ? $"{count} nouveaux contacts importés !"
                    : "Aucun nouveau contact à importer.");
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
                ShowToast("Erreur lors de l'importation.");
            }
        }

        private async Task SyncNowAsync(bool showSuccessToast = true)
        {
            if (IsSyncing)
                return;

            ServerUrl = _syncService.NormalizeServerUrl(ServerUrl);
            if (string.IsNullOrWhiteSpace(ServerUrl))
            {
                ServerConnectionStatus = "Aucune URL serveur";
                IsServerConnected = false;
                await LoadData();
                ShowToast("Renseignez l'URL du serveur");
                return;
            }

            IsSyncing = true;

            try
            {
                await _syncService.ConfigureAsync(ServerUrl);
                ServerConnectionStatus = "Connexion active";
                IsServerConnected = true;
                await _syncService.EnsureRealtimeAsync(ServerUrl);
                await PushPendingContactsAsync();
                await PullRemoteContactsAsync(false);

                if (showSuccessToast)
                    ShowToast("Synchronisation terminée");
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
                ServerConnectionStatus = "Serveur non joignable";
                IsServerConnected = false;
                ShowToast("Synchronisation impossible");
            }
            finally
            {
                await LoadData();
                IsSyncing = false;
            }
        }

        private async Task PushPendingContactsAsync()
        {
            var pendingContacts = AllContacts
                .Where(contact => contact.NeedsSync || string.IsNullOrWhiteSpace(contact.RemoteId))
                .ToList();

            foreach (var contact in pendingContacts)
            {
                await SyncContactAsync(contact, false);
            }
        }

        private async Task SyncContactAsync(Contact contact, bool showErrorToast)
        {
            if (contact == null || _isApplyingRemoteChanges)
                return;

            if (string.IsNullOrWhiteSpace(ServerUrl))
                return;

            try
            {
                await _syncService.ConfigureAsync(ServerUrl);
                var remoteContact = await _syncService.UpsertContactAsync(contact);
                ApplyRemoteContact(contact, remoteContact);
                contact.NeedsSync = false;
                contact.LastSyncedAtUtc = remoteContact.UpdatedAtUtc.UtcDateTime.ToString("o");
                await DatabaseService.SaveContactAsync(contact);
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
                contact.NeedsSync = true;
                await DatabaseService.SaveContactAsync(contact);

                if (showErrorToast)
                    ShowToast("Contact non synchronisé");
            }
        }

        private async Task PullRemoteContactsAsync(bool showErrorToast)
        {
            if (string.IsNullOrWhiteSpace(ServerUrl))
                return;

            try
            {
                await _syncService.ConfigureAsync(ServerUrl);
                var remoteContacts = await _syncService.GetContactsAsync();
                var remoteIds = new HashSet<string>(remoteContacts.Select(contact => contact.Id.ToString()));

                _isApplyingRemoteChanges = true;

                try
                {
                    foreach (var remoteContact in remoteContacts)
                    {
                        var remoteId = remoteContact.Id.ToString();
                        var localContact = AllContacts.FirstOrDefault(contact => contact.RemoteId == remoteId)
                            ?? await DatabaseService.GetContactByRemoteIdAsync(remoteId);

                        if (localContact == null)
                        {
                            localContact = new Contact();
                        }

                        ApplyRemoteContact(localContact, remoteContact);
                        localContact.NeedsSync = false;
                        localContact.LastSyncedAtUtc = remoteContact.UpdatedAtUtc.UtcDateTime.ToString("o");
                        await DatabaseService.SaveContactAsync(localContact);
                    }

                    var staleRemoteContacts = AllContacts
                        .Where(contact => !string.IsNullOrWhiteSpace(contact.RemoteId) && !remoteIds.Contains(contact.RemoteId))
                        .ToList();

                    foreach (var staleContact in staleRemoteContacts)
                    {
                        await DatabaseService.DeleteContactAsync(staleContact);
                    }
                }
                finally
                {
                    _isApplyingRemoteChanges = false;
                }

                await LoadData();
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
                if (showErrorToast)
                    ShowToast("Mise à jour distante impossible");
            }
        }

        private static void ApplyRemoteContact(Contact localContact, ContactSyncService.RemoteContactDto remoteContact)
        {
            localContact.FirstName = remoteContact.FirstName;
            localContact.LastName = remoteContact.LastName;
            localContact.Phone = remoteContact.Phone;
            localContact.Email = remoteContact.Email;
            localContact.Comment = remoteContact.Comment;
            localContact.PhotoPath = remoteContact.PhotoUrl;
            localContact.IsFavorite = remoteContact.IsFavorite;
            localContact.RemoteId = remoteContact.Id.ToString();
            localContact.RemoteVersion = remoteContact.Version;
        }

        private void OnRemoteContactsChanged()
        {
            Device.BeginInvokeOnMainThread(async () =>
            {
                await PullRemoteContactsAsync(false);
            });
        }

        private async void ShowToast(string message)
        {
            ToastMessage = message;
            await Task.Delay(3000);
            if (ToastMessage == message)
                ToastMessage = string.Empty;
        }

        private void ScheduleServerConnectionCheck()
        {
            _serverCheckCancellation?.Cancel();
            _serverCheckCancellation?.Dispose();
            _serverCheckCancellation = null;

            if (string.IsNullOrWhiteSpace(ServerUrl))
            {
                IsCheckingServerConnection = false;
                IsServerConnected = false;
                ServerConnectionStatus = "Aucune URL serveur";
                return;
            }

            var cancellation = new CancellationTokenSource();
            _serverCheckCancellation = cancellation;

            Task.Run(async () =>
            {
                try
                {
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        IsCheckingServerConnection = true;
                        IsServerConnected = false;
                        ServerConnectionStatus = "Verification...";
                    });

                    await Task.Delay(700, cancellation.Token);
                    await _syncService.CheckConnectionAsync(ServerUrl);
                    cancellation.Token.ThrowIfCancellationRequested();

                    Device.BeginInvokeOnMainThread(() =>
                    {
                        IsCheckingServerConnection = false;
                        IsServerConnected = true;
                        ServerConnectionStatus = "Connexion active";
                    });
                }
                catch (OperationCanceledException)
                {
                }
                catch
                {
                    if (!cancellation.IsCancellationRequested)
                    {
                        Device.BeginInvokeOnMainThread(() =>
                        {
                            IsCheckingServerConnection = false;
                            IsServerConnected = false;
                            ServerConnectionStatus = "Serveur non joignable";
                        });
                    }
                }
            });
        }

        private void ResetForm()
        {
            _editingId = 0;
            NewFirstName = string.Empty;
            NewLastName = string.Empty;
            NewPhone = string.Empty;
            NewEmail = string.Empty;
            NewComment = string.Empty;
            NewPhotoPath = null;
        }

        private async Task LoadData()
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

        private void FilterContacts()
        {
            var query = SearchText?.ToLower() ?? string.Empty;

            var filteredContacts = AllContacts
                .Where(contact =>
                    ((contact.FirstName ?? string.Empty) + " " + (contact.LastName ?? string.Empty))
                    .ToLower()
                    .Contains(query)
                    || (contact.Phone ?? string.Empty).Contains(query))
                .OrderBy(contact => contact.LastName ?? string.Empty)
                .ThenBy(contact => contact.FirstName ?? string.Empty)
                .ToList();

            var favoriteContacts = filteredContacts.Where(contact => contact.IsFavorite).ToList();
            var otherContacts = filteredContacts.Where(contact => !contact.IsFavorite).ToList();

            var groups = new List<IGrouping<string, Contact>>();

            if (favoriteContacts.Any())
            {
                var favoriteGroup = favoriteContacts.GroupBy(contact => "★ Favoris").FirstOrDefault();
                if (favoriteGroup != null)
                    groups.Add(favoriteGroup);
            }

            var alphabeticalGroups = otherContacts
                .GroupBy(contact =>
                {
                    var name = string.IsNullOrEmpty(contact.LastName) ? contact.FirstName : contact.LastName;
                    var letter = string.IsNullOrEmpty(name) ? "#" : name.Substring(0, 1).ToUpper();
                    if (!char.IsLetter(letter[0]))
                        letter = "#";
                    return letter;
                })
                .OrderBy(group => group.Key)
                .ToList();

            groups.AddRange(alphabeticalGroups);

            GroupedContacts.Clear();
            foreach (var group in groups)
            {
                GroupedContacts.Add(new ContactGroup(group.Key, group));
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
