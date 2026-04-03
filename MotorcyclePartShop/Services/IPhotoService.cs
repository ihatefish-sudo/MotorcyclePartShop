using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace MotorcyclePartShop.Services
{
    public interface IPhotoService
    {
        Task<string> UploadImageAsync(IFormFile file, string folderName);
    }
}