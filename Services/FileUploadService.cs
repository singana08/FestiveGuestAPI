using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FestiveGuestAPI.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace FestiveGuestAPI.Services;

public interface IFileUploadService
{
    Task<string> UploadProfileImageAsync(IFormFile file, string userId);
    Task<bool> DeleteProfileImageAsync(string fileName);
}

public class FileUploadService : IFileUploadService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly AppSecrets _secrets;

    public FileUploadService(AppSecrets secrets)
    {
        _secrets = secrets;
        _blobServiceClient = new BlobServiceClient(secrets.BlobStorageConnectionString);
    }

    public async Task<string> UploadProfileImageAsync(IFormFile file, string userId)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File is empty");

        // Validate file type
        var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp" };
        if (!allowedTypes.Contains(file.ContentType.ToLower()))
            throw new ArgumentException("Only JPEG, PNG, and WebP images are allowed");

        // Validate file size (max 10MB)
        if (file.Length > 10 * 1024 * 1024)
            throw new ArgumentException("File size must be less than 10MB");

        var containerClient = _blobServiceClient.GetBlobContainerClient(_secrets.BlobContainerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

        var fileName = $"{userId}_{Guid.NewGuid()}.jpg";
        var blobClient = containerClient.GetBlobClient(fileName);

        // Compress and resize image
        using var originalStream = file.OpenReadStream();
        using var compressedStream = new MemoryStream();
        
        using (var image = await Image.LoadAsync(originalStream))
        {
            // Resize if larger than 1200px on any side
            if (image.Width > 1200 || image.Height > 1200)
            {
                var ratio = Math.Min(1200.0 / image.Width, 1200.0 / image.Height);
                var newWidth = (int)(image.Width * ratio);
                var newHeight = (int)(image.Height * ratio);
                
                image.Mutate(x => x.Resize(newWidth, newHeight));
            }

            // Save as JPEG with high quality
            var encoder = new JpegEncoder { Quality = 85 };
            await image.SaveAsync(compressedStream, encoder);
        }

        compressedStream.Position = 0;

        await blobClient.UploadAsync(compressedStream, new BlobHttpHeaders
        {
            ContentType = "image/jpeg"
        });

        return blobClient.Uri.ToString();
    }

    public async Task<bool> DeleteProfileImageAsync(string fileName)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_secrets.BlobContainerName);
            var blobClient = containerClient.GetBlobClient(fileName);
            
            var response = await blobClient.DeleteIfExistsAsync();
            return response.Value;
        }
        catch
        {
            return false;
        }
    }
}