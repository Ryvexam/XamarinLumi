using System;
using System.IO;
using Android.App;
using Android.Content;
using Android.Provider;

class Test {
    public void GetPhoto(string id) {
        var ctx = Application.Context;
        var uri = ContentUris.WithAppendedId(ContactsContract.Contacts.ContentUri, long.Parse(id));
        var stream = ContactsContract.Contacts.OpenContactPhotoInputStream(ctx.ContentResolver, uri);
        // if stream is not null, read it and save it.
    }
}
