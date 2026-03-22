using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SQLite;
using LumiContact.ViewModels; // To reference Contact

namespace LumiContact.Services
{
    public static class DatabaseService
    {
        static SQLiteAsyncConnection _database;

        private sealed class TableColumnInfo
        {
            [Column("name")]
            public string Name { get; set; }
        }

        public static async Task Init()
        {
            if (_database != null)
                return;

            var databasePath = Path.Combine(Xamarin.Essentials.FileSystem.AppDataDirectory, "LumiContactContacts.db3");
            _database = new SQLiteAsyncConnection(databasePath);

            await _database.CreateTableAsync<Contact>();
            await EnsureColumnAsync("Contact", "RemoteId", "TEXT");
            await EnsureColumnAsync("Contact", "RemoteVersion", "INTEGER NOT NULL DEFAULT 0");
            await EnsureColumnAsync("Contact", "NeedsSync", "INTEGER NOT NULL DEFAULT 0");
            await EnsureColumnAsync("Contact", "LastSyncedAtUtc", "TEXT");
        }

        public static async Task<List<Contact>> GetContactsAsync()
        {
            await Init();
            return await _database.Table<Contact>().OrderByDescending(x => x.Id).ToListAsync();
        }

        public static async Task<int> SaveContactAsync(Contact contact)
        {
            await Init();
            if (contact.Id != 0)
                return await _database.UpdateAsync(contact);
            else
                return await _database.InsertAsync(contact);
        }

        public static async Task<int> DeleteContactAsync(Contact contact)
        {
            await Init();
            return await _database.DeleteAsync(contact);
        }

        public static async Task<Contact> GetContactByRemoteIdAsync(string remoteId)
        {
            await Init();
            return await _database.Table<Contact>()
                .Where(contact => contact.RemoteId == remoteId)
                .FirstOrDefaultAsync();
        }

        private static async Task EnsureColumnAsync(string tableName, string columnName, string definition)
        {
            var columns = await _database.QueryAsync<TableColumnInfo>($"PRAGMA table_info('{tableName}')");
            if (columns.Any(column => string.Equals(column.Name, columnName, StringComparison.OrdinalIgnoreCase)))
                return;

            await _database.ExecuteAsync($"ALTER TABLE {tableName} ADD COLUMN {columnName} {definition}");
        }
    }
}
