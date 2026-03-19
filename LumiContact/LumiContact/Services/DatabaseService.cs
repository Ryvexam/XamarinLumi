using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SQLite;
using LumiContact.ViewModels; // To reference Contact

namespace LumiContact.Services
{
    public static class DatabaseService
    {
        static SQLiteAsyncConnection _database;

        public static async Task Init()
        {
            if (_database != null)
                return;

            var databasePath = Path.Combine(Xamarin.Essentials.FileSystem.AppDataDirectory, "LumiContactContacts.db3");
            _database = new SQLiteAsyncConnection(databasePath);

            await _database.CreateTableAsync<Contact>();
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
    }
}