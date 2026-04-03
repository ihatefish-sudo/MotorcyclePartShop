using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace MotorcyclePartShop.Services
{
    public class CloudinaryPhotoService : IPhotoService
    {
        private readonly Cloudinary _cloudinary;

        public CloudinaryPhotoService(IConfiguration config)
        {
            var account = new Account(
                config["Cloudinary:CloudName"],
                config["Cloudinary:ApiKey"],
                config["Cloudinary:ApiSecret"]
            );
            _cloudinary = new Cloudinary(account);
        }

        public async Task<string> UploadImageAsync(IFormFile file, string folderName)
        {
            if (file == null || file.Length == 0) return null;

            var uploadResult = new ImageUploadResult();

            using (var stream = file.OpenReadStream())
            {
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = folderName // Tạo thư mục con trên Cloudinary cho gọn
                };

                uploadResult = await _cloudinary.UploadAsync(uploadParams);
            }

            // Trả về đường link URL an toàn (HTTPS) của bức ảnh
            return uploadResult.SecureUrl.ToString();
        }
    }
}