using LumiContact.Backend.Configuration;
using Microsoft.Extensions.Options;

namespace LumiContact.Backend.Services;

public sealed class PhotoStorageService(
    IWebHostEnvironment environment,
    IOptions<SyncServerOptions> options)
{
    private const string OwnedUploadsPathPrefix = "/uploads/contacts/";
    private readonly IWebHostEnvironment _environment = environment;
    private readonly SyncServerOptions _options = options.Value;

    public async Task<string?> SaveBase64PhotoAsync(string? rawPhoto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawPhoto))
        {
            return null;
        }

        var (base64Payload, extension) = SplitPhotoPayload(rawPhoto);
        var bytes = Convert.FromBase64String(base64Payload);

        if (bytes.LongLength > _options.MaxPhotoBytes)
        {
            throw new InvalidOperationException($"Photo exceeds the {_options.MaxPhotoBytes} byte limit.");
        }

        var webRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var uploadDirectory = Path.Combine(webRoot, "uploads", "contacts");
        Directory.CreateDirectory(uploadDirectory);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(uploadDirectory, fileName);
        await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken);

        return $"{OwnedUploadsPathPrefix}{fileName}";
    }

    public Task DeleteOwnedPhotoAsync(string? photoUrl, CancellationToken cancellationToken)
    {
        var ownedPhotoPath = TryGetOwnedPhotoPath(photoUrl);
        if (ownedPhotoPath is null)
        {
            return Task.CompletedTask;
        }

        var webRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var fullPath = Path.Combine(webRoot, ownedPhotoPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public string? NormalizeStoredPhotoUrl(string? photoUrl)
    {
        if (string.IsNullOrWhiteSpace(photoUrl))
        {
            return null;
        }

        return TryGetOwnedPhotoPath(photoUrl) ?? photoUrl;
    }

    public string? ResolvePublicPhotoUrl(string? photoUrl, string publicBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(photoUrl))
        {
            return null;
        }

        var ownedPhotoPath = TryGetOwnedPhotoPath(photoUrl);
        return ownedPhotoPath is null
            ? photoUrl
            : $"{publicBaseUrl.TrimEnd('/')}{ownedPhotoPath}";
    }

    private static (string Base64Payload, string Extension) SplitPhotoPayload(string rawPhoto)
    {
        if (!rawPhoto.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return (rawPhoto, ".jpg");
        }

        var metadataSeparator = rawPhoto.IndexOf(',');
        if (metadataSeparator < 0)
        {
            return (rawPhoto, ".jpg");
        }

        var metadata = rawPhoto[..metadataSeparator];
        var base64Payload = rawPhoto[(metadataSeparator + 1)..];
        var extension = metadata.Contains("image/png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";

        return (base64Payload, extension);
    }

    private static string? TryGetOwnedPhotoPath(string? photoUrl)
    {
        if (string.IsNullOrWhiteSpace(photoUrl))
        {
            return null;
        }

        if (Uri.TryCreate(photoUrl, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.AbsolutePath.StartsWith(OwnedUploadsPathPrefix, StringComparison.OrdinalIgnoreCase)
                ? absoluteUri.AbsolutePath
                : null;
        }

        var relativePath = photoUrl.StartsWith("/", StringComparison.Ordinal)
            ? photoUrl
            : $"/{photoUrl}";

        return relativePath.StartsWith(OwnedUploadsPathPrefix, StringComparison.OrdinalIgnoreCase)
            ? relativePath
            : null;
    }
}
