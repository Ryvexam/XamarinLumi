using System.IO;
using Android.App;
using Android.Content;
using Android.Provider;
using LumiContact.Services;
using Xamarin.Forms;

[assembly: Dependency(typeof(LumiContact.Droid.Services.ContactPhotoService))]
namespace LumiContact.Droid.Services
{
    public class ContactPhotoService : IContactPhotoService
    {
        public string GetContactPhoto(string contactId)
        {
            try
            {
                if (string.IsNullOrEmpty(contactId)) return null;

                var uri = ContentUris.WithAppendedId(ContactsContract.Contacts.ContentUri, long.Parse(contactId));
                using (var stream = ContactsContract.Contacts.OpenContactPhotoInputStream(Application.Context.ContentResolver, uri))
                {
                    if (stream == null) return null;
                    
                    var cacheDir = Xamarin.Essentials.FileSystem.CacheDirectory;
                    var fileName = $"contact_photo_{contactId}_{System.Guid.NewGuid():N}.jpg";
                    var filePath = Path.Combine(cacheDir, fileName);
                    
                    using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                    {
                        stream.CopyTo(fs);
                    }
                    
                    return filePath;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}