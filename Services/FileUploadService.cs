using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Azure.Storage;
using FestiveGuestAPI.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace FestiveGuestAPI.Services;

public interface IFileUploadService
{
    Task<string> GenerateUploadSasTokenAsync(string userId);
    Task<string> UploadProfileImageAsync(IFormFile file, string userId);
    Task<bool> DeleteProfileImageAsync(string fileName);
    string GenerateReadSasUrl(string fileName, string containerName = "logos");
}

public class FileUploadService : IFileUploadService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly StorageSharedKeyCredential _credential;
    private readonly string _accountName;
    private readonly AppSecrets _secrets;

    public FileUploadService(AppSecrets secrets)
    {
        _secrets = secrets;
        
        // Parse connection string
        var parts = secrets.BlobStorageConnectionString.Split(';');
        foreach (var part in parts)
        {
            if (part.StartsWith("AccountName="))
                _accountName = part.Replace("AccountName=", "");
            else if (part.StartsWith("AccountKey="))
            {
                var accountKey = part.Replace("AccountKey=", "");
                _credential = new StorageSharedKeyCredential(_accountName, accountKey);
            }
        }
        
        _blobServiceClient = new BlobServiceClient(new Uri($"https://{_accountName}.blob.core.windows.net"), _credential);
    }

    public async Task<string> UploadProfileImageAsync(IFormFile file, string userId)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File is empty");

        var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp" };
        if (!allowedTypes.Contains(file.ContentType.ToLower()))
            throw new ArgumentException("Only JPEG, PNG, and WebP images are allowed");

        if (file.Length > 10 * 1024 * 1024)
            throw new ArgumentException("File size must be less than 10MB");

        var containerClient = _blobServiceClient.GetBlobContainerClient("profile-images");
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

        var fileName = $"{userId}_{Guid.NewGuid()}.jpg";
        var blobClient = containerClient.GetBlobClient(fileName);

        using var originalStream = file.OpenReadStream();
        using var compressedStream = new MemoryStream();
        
        using (var image = await Image.LoadAsync(originalStream))
        {
            if (image.Width > 800 || image.Height > 800)
            {
                var ratio = Math.Min(800.0 / image.Width, 800.0 / image.Height);
                image.Mutate(x => x.Resize((int)(image.Width * ratio), (int)(image.Height * ratio)));
            }

            await image.SaveAsync(compressedStream, new JpegEncoder { Quality = 80 });
        }

        compressedStream.Position = 0;
        await blobClient.UploadAsync(compressedStream, new BlobHttpHeaders { ContentType = "image/jpeg" });

        return blobClient.Uri.ToString();
    }

    public async Task<bool> DeleteProfileImageAsync(string fileName)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient("profile-images");
            var blobClient = containerClient.GetBlobClient(fileName);
            var response = await blobClient.DeleteIfExistsAsync();
            return response.Value;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GenerateUploadSasTokenAsync(string userId)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient("profile-images");
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

        var blobName = $"{userId}_{Guid.NewGuid()}.jpg";

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = "profile-images",
            BlobName = blobName,
            Resource = "b",
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(30)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Write | BlobSasPermissions.Create | BlobSasPermissions.Read);

        var sasToken = sasBuilder.ToSasQueryParameters(_credential).ToString();
        var blobUrl = $"https://{_accountName}.blob.core.windows.net/profile-images/{blobName}";
        
        return $"{blobUrl}?{sasToken}";
    }

    public string GenerateReadSasUrl(string fileName, string containerName = "logos")
    {
        try
        {
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = containerName,
                BlobName = fileName,
                Resource = "b",
                StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
            };
            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            var sasToken = sasBuilder.ToSasQueryParameters(_credential).ToString();
            var blobUrl = $"https://{_accountName}.blob.core.windows.net/{containerName}/{fileName}";
            
            return $"{blobUrl}?{sasToken}";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating SAS URL: {ex.Message}");
            return $"https://{_accountName}.blob.core.windows.net/{containerName}/{fileName}";
        }
    }
}