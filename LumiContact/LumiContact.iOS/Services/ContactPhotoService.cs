using System.IO;
using Contacts;
using Foundation;
using LumiContact.Services;
using Xamarin.Forms;

[assembly: Dependency(typeof(LumiContact.iOS.Services.ContactPhotoService))]
namespace LumiContact.iOS.Services
{
    public class ContactPhotoService : IContactPhotoService
    {
        public string GetContactPhoto(string contactId)
        {
            try
            {
                if (string.IsNullOrEmpty(contactId)) return null;

                var store = new CNContactStore();
                var keysToFetch = new[] { CNContactKey.ImageData };
                var contact = store.GetUnifiedContact(contactId, keysToFetch, out var error);
                
                if (contact != null && contact.ImageData != null)
                {
                    var cacheDir = Xamarin.Essentials.FileSystem.CacheDirectory;
                    var fileName = $"contact_photo_{contactId}_{System.Guid.NewGuid():N}.jpg";
                    var filePath = Path.Combine(cacheDir, fileName);
                    
                    contact.ImageData.Save(filePath, true);
                    return filePath;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}