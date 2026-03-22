using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace LumiContact.Backend.Services;

public sealed class ContactsWebSocketNotifier
{
    private readonly ConcurrentDictionary<Guid, WebSocket> _sockets = new();

    public Guid Add(WebSocket socket)
    {
        var id = Guid.NewGuid();
        _sockets[id] = socket;
        return id;
    }

    public void Remove(Guid id)
    {
        _sockets.TryRemove(id, out _);
    }

    public async Task BroadcastAsync(string payload, CancellationToken cancellationToken)
    {
        var messageBytes = Encoding.UTF8.GetBytes(payload);
        var deadSockets = new List<Guid>();

        foreach (var pair in _sockets)
        {
            var socket = pair.Value;
            if (socket.State != WebSocketState.Open)
            {
                deadSockets.Add(pair.Key);
                continue;
            }

            try
            {
                await socket.SendAsync(
                    new ArraySegment<byte>(messageBytes),
                    WebSocketMessageType.Text,
                    true,
                    cancellationToken);
            }
            catch
            {
                deadSockets.Add(pair.Key);
            }
        }

        foreach (var deadSocket in deadSockets)
        {
            Remove(deadSocket);
        }
    }
}
