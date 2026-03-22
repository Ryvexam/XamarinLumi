namespace LumiContact.Backend.Configuration;

public sealed class SyncServerOptions
{
    public const string SectionName = "SyncServer";

    public string PublicBaseUrl { get; set; } = "http://localhost:8080";

    public string AppKey { get; set; } = "lumicontact-public-app";

    public long MaxPhotoBytes { get; set; } = 2 * 1024 * 1024;

    public string TrimmedPublicBaseUrl => PublicBaseUrl.TrimEnd('/');
}
