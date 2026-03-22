using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Xamarin.Essentials;
using LumiContact.ViewModels;
using AppContact = LumiContact.ViewModels.Contact;

namespace LumiContact.Services
{
    public sealed class ContactSyncService
    {
        private const string DefaultAppKey = "lumicontact-public-app";
        private const string AppKeyPreference = "syncAppKey";
        private const string AppKeyHeader = "X-Lumi-App-Key";

        private static readonly Lazy<ContactSyncService> LazyInstance =
            new Lazy<ContactSyncService>(() => new ContactSyncService());

        private readonly HttpClient _httpClient;
        private string _configuredServerUrl;
        private ClientWebSocket _webSocket;
        private CancellationTokenSource _webSocketCancellation;
        private Task _webSocketListener;

        private ContactSyncService()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(20)
            };
        }

        public static ContactSyncService Instance => LazyInstance.Value;

        public event Action RemoteContactsChanged;

        public Task ConfigureAsync(string serverUrl)
        {
            var normalizedServerUrl = NormalizeServerUrl(serverUrl);
            if (string.Equals(_configuredServerUrl, normalizedServerUrl, StringComparison.OrdinalIgnoreCase))
                return Task.CompletedTask;

            _configuredServerUrl = normalizedServerUrl;
            _httpClient.DefaultRequestHeaders.Remove(AppKeyHeader);

            if (string.IsNullOrWhiteSpace(_configuredServerUrl))
            {
                _httpClient.BaseAddress = null;
                return Task.CompletedTask;
            }

            _httpClient.BaseAddress = new Uri(_configuredServerUrl, UriKind.Absolute);
            _httpClient.DefaultRequestHeaders.Add(AppKeyHeader, GetAppKey());
            return Task.CompletedTask;
        }

        public async Task<List<RemoteContactDto>> GetContactsAsync()
        {
            EnsureConfigured();

            var response = await _httpClient.GetAsync("api/contacts");
            await EnsureSuccessAsync(response);
            var payload = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<RemoteContactDto>>(payload) ?? new List<RemoteContactDto>();
        }

        public async Task<RemoteContactDto> UpsertContactAsync(AppContact contact)
        {
            EnsureConfigured();

            var request = BuildPayload(contact);
            var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
            HttpResponseMessage response;

            if (string.IsNullOrWhiteSpace(contact.RemoteId))
            {
                response = await _httpClient.PostAsync("api/contacts", content);
            }
            else
            {
                response = await _httpClient.PutAsync($"api/contacts/{contact.RemoteId}", content);
            }

            await EnsureSuccessAsync(response);
            var payload = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<RemoteContactDto>(payload);
        }

        public async Task DeleteContactAsync(AppContact contact)
        {
            EnsureConfigured();

            if (string.IsNullOrWhiteSpace(contact.RemoteId))
                return;

            var response = await _httpClient.DeleteAsync($"api/contacts/{contact.RemoteId}");
            await EnsureSuccessAsync(response);
        }

        public async Task CheckConnectionAsync(string serverUrl)
        {
            await ConfigureAsync(serverUrl);
            EnsureConfigured();

            var response = await _httpClient.GetAsync("api/health");
            await EnsureSuccessAsync(response);
            await EnsureRealtimeAsync(serverUrl);
        }

        public async Task EnsureRealtimeAsync(string serverUrl)
        {
            await ConfigureAsync(serverUrl);

            if (string.IsNullOrWhiteSpace(_configuredServerUrl))
                return;

            if (_webSocket != null && _webSocket.State == WebSocketState.Open)
                return;

            await DisconnectAsync();

            var socketUrl = BuildWebSocketUrl(_configuredServerUrl);
            var webSocket = new ClientWebSocket();
            webSocket.Options.SetRequestHeader(AppKeyHeader, GetAppKey());

            var cancellation = new CancellationTokenSource();
            await webSocket.ConnectAsync(new Uri(socketUrl), cancellation.Token);

            _webSocket = webSocket;
            _webSocketCancellation = cancellation;
            _webSocketListener = Task.Run(() => ListenForRealtimeUpdatesAsync(webSocket, cancellation.Token));
        }

        public async Task DisconnectAsync()
        {
            var webSocket = _webSocket;
            var cancellation = _webSocketCancellation;

            _webSocket = null;
            _webSocketCancellation = null;
            _webSocketListener = null;

            if (cancellation != null)
            {
                cancellation.Cancel();
                cancellation.Dispose();
            }

            if (webSocket == null)
                return;

            try
            {
                if (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.CloseReceived)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
            }
            catch
            {
                // Ignore close issues during reconfiguration.
            }
            finally
            {
                webSocket.Dispose();
            }
        }

        public string NormalizeServerUrl(string serverUrl)
        {
            if (string.IsNullOrWhiteSpace(serverUrl))
                return string.Empty;

            var normalizedServerUrl = serverUrl.Trim();
            if (!normalizedServerUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !normalizedServerUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                normalizedServerUrl = "https://" + normalizedServerUrl;
            }

            return normalizedServerUrl.TrimEnd('/');
        }

        private static string GetAppKey()
        {
            return Preferences.Get(AppKeyPreference, DefaultAppKey);
        }

        private RemoteUpsertContactRequest BuildPayload(AppContact contact)
        {
            var payload = new RemoteUpsertContactRequest
            {
                FirstName = contact.FirstName,
                LastName = contact.LastName,
                Phone = contact.Phone,
                Email = contact.Email,
                Comment = contact.Comment,
                PhotoUrl = IsRemotePhoto(contact.PhotoPath) ? contact.PhotoPath : null,
                ClearPhoto = string.IsNullOrWhiteSpace(contact.PhotoPath),
                IsFavorite = contact.IsFavorite
            };

            if (!string.IsNullOrWhiteSpace(contact.PhotoPath)
                && !IsRemotePhoto(contact.PhotoPath)
                && File.Exists(contact.PhotoPath))
            {
                var bytes = File.ReadAllBytes(contact.PhotoPath);
                var extension = Path.GetExtension(contact.PhotoPath)?.ToLowerInvariant();
                var mimeType = extension == ".png" ? "image/png" : "image/jpeg";
                payload.PhotoBase64 = $"data:{mimeType};base64,{Convert.ToBase64String(bytes)}";
            }

            return payload;
        }

        private static bool IsRemotePhoto(string photoPath)
        {
            return !string.IsNullOrWhiteSpace(photoPath)
                && (photoPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    || photoPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
        }

        private async Task ListenForRealtimeUpdatesAsync(ClientWebSocket webSocket, CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];

            try
            {
                while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        WebSocketReceiveResult result;

                        do
                        {
                            result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                await ReconnectRealtimeAsync();
                                return;
                            }

                            memoryStream.Write(buffer, 0, result.Count);
                        }
                        while (!result.EndOfMessage);

                        var payload = Encoding.UTF8.GetString(memoryStream.ToArray());
                        if (!string.IsNullOrWhiteSpace(payload))
                        {
                            try
                            {
                                JsonConvert.DeserializeObject<RemoteContactsChangedMessage>(payload);
                                RemoteContactsChanged?.Invoke();
                            }
                            catch
                            {
                                // Ignore malformed websocket messages.
                            }
                        }
                    }
                }
            }
            catch
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    await ReconnectRealtimeAsync();
                }
            }
        }

        private async Task ReconnectRealtimeAsync()
        {
            var serverUrl = _configuredServerUrl;
            if (string.IsNullOrWhiteSpace(serverUrl))
                return;

            try
            {
                await Task.Delay(1500);
                await EnsureRealtimeAsync(serverUrl);
            }
            catch
            {
                // Ignore transient realtime reconnect errors.
            }
        }

        private static string BuildWebSocketUrl(string serverUrl)
        {
            var baseUrl = serverUrl.TrimEnd('/');
            if (baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return "wss://" + baseUrl.Substring("https://".Length) + "/ws/contacts";
            }

            if (baseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                return "ws://" + baseUrl.Substring("http://".Length) + "/ws/contacts";
            }

            return baseUrl + "/ws/contacts";
        }

        private void EnsureConfigured()
        {
            if (string.IsNullOrWhiteSpace(_configuredServerUrl))
                throw new InvalidOperationException("Sync service is not configured.");
        }

        private static async Task EnsureSuccessAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
                return;

            var details = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Sync request failed: {(int)response.StatusCode} {response.ReasonPhrase} {details}");
        }

        public sealed class RemoteContactDto
        {
            public Guid Id { get; set; }

            public string FirstName { get; set; }

            public string LastName { get; set; }

            public string Phone { get; set; }

            public string Email { get; set; }

            public string Comment { get; set; }

            public string PhotoUrl { get; set; }

            public bool IsFavorite { get; set; }

            public long Version { get; set; }

            public DateTimeOffset UpdatedAtUtc { get; set; }
        }

        private sealed class RemoteUpsertContactRequest
        {
            public string FirstName { get; set; }

            public string LastName { get; set; }

            public string Phone { get; set; }

            public string Email { get; set; }

            public string Comment { get; set; }

            public string PhotoUrl { get; set; }

            public string PhotoBase64 { get; set; }

            public bool ClearPhoto { get; set; }

            public bool IsFavorite { get; set; }
        }

        private sealed class RemoteContactsChangedMessage
        {
            public string Action { get; set; }

            public Guid ContactId { get; set; }

            public DateTimeOffset OccurredAtUtc { get; set; }
        }
    }
}
