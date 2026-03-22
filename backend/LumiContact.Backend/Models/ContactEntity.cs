using System.ComponentModel.DataAnnotations;

namespace LumiContact.Backend.Models;

public sealed class ContactEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Comment { get; set; } = string.Empty;

    public string? PhotoUrl { get; set; }

    public bool IsFavorite { get; set; }

    public long Version { get; set; } = 1;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
