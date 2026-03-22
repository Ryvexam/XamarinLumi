using LumiContact.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace LumiContact.Backend.Data;

public sealed class ContactsDbContext(DbContextOptions<ContactsDbContext> options) : DbContext(options)
{
    public DbSet<ContactEntity> Contacts => Set<ContactEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ContactEntity>(entity =>
        {
            entity.ToTable("Contacts");
            entity.HasKey(contact => contact.Id);
            entity.Property(contact => contact.FirstName).HasMaxLength(120);
            entity.Property(contact => contact.LastName).HasMaxLength(120);
            entity.Property(contact => contact.Phone).HasMaxLength(64);
            entity.Property(contact => contact.Email).HasMaxLength(256);
            entity.Property(contact => contact.Comment).HasMaxLength(2048);
            entity.Property(contact => contact.PhotoUrl).HasMaxLength(2048);
        });
    }
}
