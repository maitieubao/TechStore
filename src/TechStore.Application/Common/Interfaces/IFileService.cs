namespace TechStore.Application.Common.Interfaces
{
    public interface IFileService
    {
        Task<string> UploadFileAsync(Stream fileStream, string fileName, string folder = "products");
        Task<bool> DeleteFileAsync(string filePath);
    }
}
