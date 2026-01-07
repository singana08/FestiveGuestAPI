using Azure.Security.KeyVault.Secrets;
using FestiveGuestAPI.Configuration;

namespace FestiveGuestAPI.Services;

public class SecretService
{
    private readonly SecretClient _secretClient;

    public SecretService(SecretClient secretClient)
    {
        _secretClient = secretClient;
    }

    public async Task<AppSecrets> LoadSecretsAsync()
    {
        var secrets = new AppSecrets();

        secrets.TableStorageConnectionString = (await _secretClient.GetSecretAsync("storage-connection-string")).Value.Value;
        secrets.BlobStorageConnectionString = (await _secretClient.GetSecretAsync("storage-connection-string")).Value.Value;
        secrets.JwtSecretKey = (await _secretClient.GetSecretAsync("jwt-secret")).Value.Value;
        secrets.JwtIssuer = "FestiveGuestAPI";
        secrets.JwtAudience = "FestiveGuestApp";
        
        // Optional blob container names - use defaults if not in Key Vault
        try { secrets.BlobContainerName = (await _secretClient.GetSecretAsync("BlobContainerName")).Value.Value; }
        catch { secrets.BlobContainerName = "profile-images"; }
        
        try { secrets.BlobLogoContainerName = (await _secretClient.GetSecretAsync("BlobLogoContainerName")).Value.Value; }
        catch { secrets.BlobLogoContainerName = "logos"; }
        
        secrets.SmtpHost = (await _secretClient.GetSecretAsync("smtp-host")).Value.Value;
        secrets.SmtpPort = (await _secretClient.GetSecretAsync("smtp-port")).Value.Value;
        secrets.SmtpUsername = (await _secretClient.GetSecretAsync("smtp-username")).Value.Value;
        secrets.SmtpPassword = (await _secretClient.GetSecretAsync("smtp-password")).Value.Value;
        secrets.FromEmailAddress = (await _secretClient.GetSecretAsync("from-email")).Value.Value;
        secrets.AcsConnectionString = (await _secretClient.GetSecretAsync("acs-connection-string")).Value.Value;

        return secrets;
    }
}