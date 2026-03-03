using Microsoft.AspNetCore.Hosting;
using TechStore.Application.Common.Interfaces;

namespace TechStore.Infrastructure.Services
{
    public class LocalFileService : IFileService
    {
        private readonly string _basePath;
        private readonly string _baseUrl;

        public LocalFileService(IWebHostEnvironment env)
        {
            _basePath = Path.Combine(env.ContentRootPath, "wwwroot", "uploads");
            _baseUrl = "/uploads";

            if (!Directory.Exists(_basePath))
                Directory.CreateDirectory(_basePath);
        }

        public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string folder = "products")
        {
            var folderPath = Path.Combine(_basePath, folder);
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            // Generate unique filename
            var extension = Path.GetExtension(fileName);
            var uniqueName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(folderPath, uniqueName);

            using var outputStream = new FileStream(filePath, FileMode.Create);
            await fileStream.CopyToAsync(outputStream);

            return $"{_baseUrl}/{folder}/{uniqueName}";
        }

        public Task<bool> DeleteFileAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return Task.FromResult(false);

            // Convert URL to physical path
            var relativePath = filePath.TrimStart('/');
            var fullPath = Path.Combine(Path.GetDirectoryName(_basePath)!, relativePath);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
    }
}
