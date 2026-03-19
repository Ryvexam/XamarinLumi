using System.IO;
using System.Threading.Tasks;
using Android.Graphics;
using LumiContact.Services;
using LumiContact.Droid.Services;
using Xamarin.Forms;

[assembly: Dependency(typeof(ImageResizer))]
namespace LumiContact.Droid.Services
{
    public class ImageResizer : IImageResizer
    {
        public Task<byte[]> ResizeImageAsync(byte[] imageData, float width, float height)
        {
            return Task.Run(() =>
            {
                // Load original bitmap
                Bitmap originalImage = BitmapFactory.DecodeByteArray(imageData, 0, imageData.Length);
                
                float oldWidth = (float)originalImage.Width;
                float oldHeight = (float)originalImage.Height;
                float scaleFactor = 1f;

                if (oldWidth > oldHeight)
                {
                    scaleFactor = width / oldWidth;
                }
                else
                {
                    scaleFactor = height / oldHeight;
                }

                int newWidth = (int)(oldWidth * scaleFactor);
                int newHeight = (int)(oldHeight * scaleFactor);

                Bitmap resizedImage = Bitmap.CreateScaledBitmap(originalImage, newWidth, newHeight, false);

                using (MemoryStream ms = new MemoryStream())
                {
                    resizedImage.Compress(Bitmap.CompressFormat.Jpeg, 80, ms);
                    return ms.ToArray();
                }
            });
        }
    }
}