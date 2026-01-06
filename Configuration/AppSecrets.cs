namespace FestiveGuestAPI.Configuration;

public class AppSecrets
{
    public string TableStorageConnectionString { get; set; } = string.Empty;
    public string JwtSecretKey { get; set; } = string.Empty;
    public string JwtIssuer { get; set; } = string.Empty;
    public string JwtAudience { get; set; } = string.Empty;
    public string BlobStorageConnectionString { get; set; } = string.Empty;
    public string BlobContainerName { get; set; } = string.Empty;
    public string BlobLogoContainerName { get; set; } = string.Empty;
    public string BlobLoginLogoContainerName { get; set; } = string.Empty;
    public string SmtpHost { get; set; } = string.Empty;
    public string SmtpPort { get; set; } = string.Empty;
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public string FromEmailAddress { get; set; } = string.Empty;
    public string AcsConnectionString { get; set; } = string.Empty;
    public string AcsEndpoint { get; set; } = string.Empty;
}