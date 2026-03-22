using System.Security.Cryptography;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using LumiContact.Backend.Configuration;
using LumiContact.Backend.Contracts;
using LumiContact.Backend.Data;
using LumiContact.Backend.Hubs;
using LumiContact.Backend.Models;
using LumiContact.Backend.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var webRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
Directory.CreateDirectory(webRootPath);

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = webRootPath
});

builder.Services.Configure<SyncServerOptions>(
    builder.Configuration.GetSection(SyncServerOptions.SectionName));

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=/data/lumicontact.db";

builder.Services.AddDbContext<ContactsDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddSingleton<PhotoStorageService>();
builder.Services.AddSingleton<ContactsWebSocketNotifier>();
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetIsOriginAllowed(_ => true);
    });
});

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
app.UseCors();
app.UseStaticFiles();
app.UseWebSockets();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ContactsDbContext>();
    await db.Database.EnsureCreatedAsync();
}

var syncOptions = app.Services.GetRequiredService<IOptions<SyncServerOptions>>().Value;
if (string.IsNullOrWhiteSpace(syncOptions.AppKey))
{
    app.Logger.LogWarning("SyncServer:AppKey is empty. API requests will be rejected.");
}

app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    var requiresAppKey =
        path.StartsWithSegments("/api") && !path.StartsWithSegments("/api/health")
        || path.StartsWithSegments("/hubs");

    if (!requiresAppKey)
    {
        await next();
        return;
    }

    var providedKey = context.Request.Headers["X-Lumi-App-Key"].FirstOrDefault()
        ?? context.Request.Query["appKey"].FirstOrDefault();

    if (!MatchesAppKey(providedKey, syncOptions.AppKey))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = "Invalid app key." });
        return;
    }

    await next();
});

app.MapGet("/", (HttpContext context, IOptions<SyncServerOptions> options) => Results.Ok(new
{
    name = "LumiContact Sync Server",
    publicBaseUrl = GetPublicBaseUrl(context, options.Value),
    health = "/api/health",
    contacts = "/api/contacts",
    hub = "/hubs/contacts"
}));

app.MapGet("/api/health", () => Results.Ok(new
{
    ok = true,
    utc = DateTimeOffset.UtcNow
}));

app.MapGet("/api/settings", (HttpContext context, IOptions<SyncServerOptions> options) => Results.Ok(new
{
    publicBaseUrl = GetPublicBaseUrl(context, options.Value),
    hubUrl = $"{GetPublicBaseUrl(context, options.Value)}/hubs/contacts",
    wsUrl = BuildWebSocketUrl(GetPublicBaseUrl(context, options.Value))
}));

app.MapGet("/api/contacts", async (
    ContactsDbContext db,
    PhotoStorageService photoStorage,
    HttpContext context,
    IOptions<SyncServerOptions> options,
    CancellationToken cancellationToken) =>
{
    var publicBaseUrl = GetPublicBaseUrl(context, options.Value);
    var contacts = await db.Contacts
        .OrderBy(contact => contact.LastName)
        .ThenBy(contact => contact.FirstName)
        .ToListAsync(cancellationToken);

    return Results.Ok(contacts.Select(contact =>
        contact.ToDto(photoStorage.ResolvePublicPhotoUrl(contact.PhotoUrl, publicBaseUrl))));
});

app.MapPost("/api/contacts", async (
    UpsertContactRequest request,
    ContactsDbContext db,
    PhotoStorageService photoStorage,
    HttpContext context,
    IOptions<SyncServerOptions> options,
    IHubContext<ContactsHub> hubContext,
    ContactsWebSocketNotifier webSocketNotifier,
    CancellationToken cancellationToken) =>
{
    var contact = new ContactEntity();
    ApplyContactPayload(contact, request);
    contact.PhotoUrl = await ResolvePhotoUrlAsync(request, null, photoStorage, cancellationToken);

    db.Contacts.Add(contact);
    await db.SaveChangesAsync(cancellationToken);
    await BroadcastContactsChangedAsync(hubContext, webSocketNotifier, "created", contact.Id, cancellationToken);

    var publicBaseUrl = GetPublicBaseUrl(context, options.Value);
    return Results.Created($"/api/contacts/{contact.Id}",
        contact.ToDto(photoStorage.ResolvePublicPhotoUrl(contact.PhotoUrl, publicBaseUrl)));
});

app.MapPut("/api/contacts/{id:guid}", async (
    Guid id,
    UpsertContactRequest request,
    ContactsDbContext db,
    PhotoStorageService photoStorage,
    HttpContext context,
    IOptions<SyncServerOptions> options,
    IHubContext<ContactsHub> hubContext,
    ContactsWebSocketNotifier webSocketNotifier,
    CancellationToken cancellationToken) =>
{
    var contact = await db.Contacts.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    if (contact is null)
    {
        return Results.NotFound();
    }

    var previousPhotoUrl = contact.PhotoUrl;
    ApplyContactPayload(contact, request);
    contact.PhotoUrl = await ResolvePhotoUrlAsync(request, previousPhotoUrl, photoStorage, cancellationToken);
    contact.Version += 1;
    contact.UpdatedAtUtc = DateTimeOffset.UtcNow;

    await db.SaveChangesAsync(cancellationToken);
    await BroadcastContactsChangedAsync(hubContext, webSocketNotifier, "updated", contact.Id, cancellationToken);

    var publicBaseUrl = GetPublicBaseUrl(context, options.Value);
    return Results.Ok(contact.ToDto(photoStorage.ResolvePublicPhotoUrl(contact.PhotoUrl, publicBaseUrl)));
});

app.MapDelete("/api/contacts/{id:guid}", async (
    Guid id,
    ContactsDbContext db,
    PhotoStorageService photoStorage,
    IHubContext<ContactsHub> hubContext,
    ContactsWebSocketNotifier webSocketNotifier,
    CancellationToken cancellationToken) =>
{
    var contact = await db.Contacts.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    if (contact is null)
    {
        return Results.NotFound();
    }

    db.Contacts.Remove(contact);
    await db.SaveChangesAsync(cancellationToken);
    await photoStorage.DeleteOwnedPhotoAsync(contact.PhotoUrl, cancellationToken);
    await BroadcastContactsChangedAsync(hubContext, webSocketNotifier, "deleted", id, cancellationToken);

    return Results.NoContent();
});

app.Map("/ws/contacts", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    var notifier = context.RequestServices.GetRequiredService<ContactsWebSocketNotifier>();
    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    var socketId = notifier.Add(socket);
    var buffer = new byte[1024];

    try
    {
        while (socket.State == WebSocketState.Open && !context.RequestAborted.IsCancellationRequested)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), context.RequestAborted);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }
        }
    }
    catch
    {
        // Connection ended unexpectedly.
    }
    finally
    {
        notifier.Remove(socketId);

        if (socket.State != WebSocketState.Closed && socket.State != WebSocketState.Aborted)
        {
            try
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
            catch
            {
                // Ignore shutdown issues.
            }
        }
    }
});

app.MapHub<ContactsHub>("/hubs/contacts");

app.Run();

static bool MatchesAppKey(string? providedKey, string expectedKey)
{
    if (string.IsNullOrWhiteSpace(providedKey) || string.IsNullOrWhiteSpace(expectedKey))
    {
        return false;
    }

    var providedBytes = Encoding.UTF8.GetBytes(providedKey);
    var expectedBytes = Encoding.UTF8.GetBytes(expectedKey);

    return providedBytes.Length == expectedBytes.Length
        && CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
}

static void ApplyContactPayload(ContactEntity contact, UpsertContactRequest request)
{
    contact.FirstName = request.FirstName?.Trim() ?? string.Empty;
    contact.LastName = request.LastName?.Trim() ?? string.Empty;
    contact.Phone = request.Phone?.Trim() ?? string.Empty;
    contact.Email = request.Email?.Trim() ?? string.Empty;
    contact.Comment = request.Comment?.Trim() ?? string.Empty;
    contact.IsFavorite = request.IsFavorite;
}

static async Task<string?> ResolvePhotoUrlAsync(
    UpsertContactRequest request,
    string? previousPhotoUrl,
    PhotoStorageService photoStorage,
    CancellationToken cancellationToken)
{
    if (!string.IsNullOrWhiteSpace(request.PhotoBase64))
    {
        var photoUrl = await photoStorage.SaveBase64PhotoAsync(request.PhotoBase64, cancellationToken);
        await photoStorage.DeleteOwnedPhotoAsync(previousPhotoUrl, cancellationToken);
        return photoUrl;
    }

    if (!string.IsNullOrWhiteSpace(request.PhotoUrl))
    {
        return photoStorage.NormalizeStoredPhotoUrl(request.PhotoUrl);
    }

    if (request.ClearPhoto)
    {
        await photoStorage.DeleteOwnedPhotoAsync(previousPhotoUrl, cancellationToken);
        return null;
    }

    return previousPhotoUrl;
}

static Task BroadcastContactsChangedAsync(
    IHubContext<ContactsHub> hubContext,
    ContactsWebSocketNotifier webSocketNotifier,
    string action,
    Guid contactId,
    CancellationToken cancellationToken)
{
    var payload = new ContactsChangedMessage
    {
        Action = action,
        ContactId = contactId,
        OccurredAtUtc = DateTimeOffset.UtcNow
    };

    var jsonPayload = JsonSerializer.Serialize(payload);
    return Task.WhenAll(
        hubContext.Clients.All.SendAsync("ContactsChanged", payload, cancellationToken),
        webSocketNotifier.BroadcastAsync(jsonPayload, cancellationToken));
}

static string GetPublicBaseUrl(HttpContext context, SyncServerOptions options)
{
    if (!ShouldUseRequestBaseUrl(options.PublicBaseUrl))
    {
        return options.TrimmedPublicBaseUrl;
    }

    return $"{context.Request.Scheme}://{context.Request.Host.Value}".TrimEnd('/');
}

static bool ShouldUseRequestBaseUrl(string? configuredPublicBaseUrl)
{
    if (string.IsNullOrWhiteSpace(configuredPublicBaseUrl))
    {
        return true;
    }

    if (!Uri.TryCreate(configuredPublicBaseUrl, UriKind.Absolute, out var uri))
    {
        return true;
    }

    return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || uri.Host.Equals("0.0.0.0", StringComparison.OrdinalIgnoreCase)
        || uri.Host.Equals("::1", StringComparison.OrdinalIgnoreCase);
}

static string BuildWebSocketUrl(string publicBaseUrl)
{
    if (publicBaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    {
        return "wss://" + publicBaseUrl.Substring("https://".Length) + "/ws/contacts";
    }

    if (publicBaseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
    {
        return "ws://" + publicBaseUrl.Substring("http://".Length) + "/ws/contacts";
    }

    return publicBaseUrl.TrimEnd('/') + "/ws/contacts";
}
