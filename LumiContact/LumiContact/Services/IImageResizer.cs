using System.Threading.Tasks;

namespace LumiContact.Services
{
    public interface IImageResizer
    {
        Task<byte[]> ResizeImageAsync(byte[] imageData, float width, float height);
    }
}