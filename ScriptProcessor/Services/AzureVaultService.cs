using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace ScriptProcessor.Services
{
    public class AzureVaultService
    {
        private readonly SecretClient _secretClient;
        private readonly ILogger<AzureVaultService> _logger;

        public AzureVaultService(IConfiguration configuration, ILogger<AzureVaultService> logger)
        {
            _logger = logger;
            var keyVaultUrl = configuration["Azure:KeyVault:Url"];

            if (string.IsNullOrEmpty(keyVaultUrl))
            {
                throw new InvalidOperationException("Azure KeyVault URL not configured");
            }

            _secretClient = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());
        }

        public string GetSecret(string secretName)
        {
            try
            {
                var secret = _secretClient.GetSecret(secretName);
                return secret.Value.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve secret: {SecretName}", secretName);
                throw;
            }
        }

        public async Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
        {
            try
            {
                var secret = await _secretClient.GetSecretAsync(secretName, cancellationToken: cancellationToken);
                return secret.Value.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve secret: {SecretName}", secretName);
                throw;
            }
        }
    }
}
