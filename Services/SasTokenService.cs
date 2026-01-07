using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using FestiveGuestAPI.Configuration;

namespace FestiveGuestAPI.Services;

public interface ISasTokenService
{
    string GenerateSasUrl(string fileName, string containerName);
}

public class SasTokenService : ISasTokenService
{
    private readonly string _accountName = string.Empty;
    private readonly string _accountKey = string.Empty;

    public SasTokenService(AppSecrets secrets)
    {
        // Parse connection string to get account name and key
        var parts = secrets.BlobStorageConnectionString.Split(';');
        foreach (var part in parts)
        {
            if (part.StartsWith("AccountName="))
                _accountName = part.Replace("AccountName=", "");
            else if (part.StartsWith("AccountKey="))
                _accountKey = part.Replace("AccountKey=", "");
        }
    }

    public string GenerateSasUrl(string fileName, string containerName)
    {
        if (string.IsNullOrEmpty(_accountName) || string.IsNullOrEmpty(_accountKey))
        {
            throw new InvalidOperationException("Storage account credentials not configured");
        }

        var blobUri = new Uri($"https://{_accountName}.blob.core.windows.net/{containerName}/{fileName}");

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = containerName,
            BlobName = fileName,
            Resource = "b",
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var storageSharedKeyCredential = new StorageSharedKeyCredential(_accountName, _accountKey);
        var sasToken = sasBuilder.ToSasQueryParameters(storageSharedKeyCredential).ToString();

        return $"{blobUri}?{sasToken}";
    }
}
