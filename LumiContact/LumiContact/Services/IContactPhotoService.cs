using System;

namespace LumiContact.Services
{
    public interface IContactPhotoService
    {
        string GetContactPhoto(string contactId);
    }
}
