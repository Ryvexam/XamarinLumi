using System;
using System.IO;
using System.Threading.Tasks;
using CoreGraphics;
using Foundation;
using UIKit;
using LumiContact.Services;
using LumiContact.iOS.Services;
using Xamarin.Forms;

[assembly: Dependency(typeof(ImageResizer))]
namespace LumiContact.iOS.Services
{
    public class ImageResizer : IImageResizer
    {
        public Task<byte[]> ResizeImageAsync(byte[] imageData, float width, float height)
        {
            return Task.Run(() =>
            {
                UIImage originalImage = new UIImage(NSData.FromArray(imageData));
                
                float oldWidth = (float)originalImage.Size.Width;
                float oldHeight = (float)originalImage.Size.Height;
                float scaleFactor = 1f;

                if (oldWidth > oldHeight)
                {
                    scaleFactor = width / oldWidth;
                }
                else
                {
                    scaleFactor = height / oldHeight;
                }

                nfloat newWidth = oldWidth * scaleFactor;
                nfloat newHeight = oldHeight * scaleFactor;

                UIGraphics.BeginImageContext(new CGSize(newWidth, newHeight));
                originalImage.Draw(new CGRect(0, 0, newWidth, newHeight));
                UIImage resizedImage = UIGraphics.GetImageFromCurrentImageContext();
                UIGraphics.EndImageContext();

                var bytes = resizedImage.AsJPEG(0.8f).ToArray();
                return bytes;
            });
        }
    }
}