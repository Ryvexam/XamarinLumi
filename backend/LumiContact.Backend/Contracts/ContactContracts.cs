using LumiContact.Backend.Models;

namespace LumiContact.Backend.Contracts;

public sealed class ContactDto
{
    public Guid Id { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Comment { get; set; } = string.Empty;

    public string? PhotoUrl { get; set; }

    public bool IsFavorite { get; set; }

    public long Version { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class UpsertContactRequest
{
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Comment { get; set; }

    public string? PhotoUrl { get; set; }

    public string? PhotoBase64 { get; set; }

    public bool ClearPhoto { get; set; }

    public bool IsFavorite { get; set; }
}

public sealed class ContactsChangedMessage
{
    public string Action { get; set; } = "updated";

    public Guid ContactId { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }
}

public static class ContactMappings
{
    public static ContactDto ToDto(this ContactEntity entity, string? photoUrlOverride = null)
    {
        return new ContactDto
        {
            Id = entity.Id,
            FirstName = entity.FirstName,
            LastName = entity.LastName,
            Phone = entity.Phone,
            Email = entity.Email,
            Comment = entity.Comment,
            PhotoUrl = photoUrlOverride ?? entity.PhotoUrl,
            IsFavorite = entity.IsFavorite,
            Version = entity.Version,
            UpdatedAtUtc = entity.UpdatedAtUtc
        };
    }
}
