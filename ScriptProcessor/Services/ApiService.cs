using System.Text;
using System.Text.Json;

namespace ScriptProcessor.Services
{
    public class ApiService : IApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ApiService> _logger;
        private readonly string _jwtSecret;
        private string? _cachedToken;
        private DateTime _tokenExpiry;

        public ApiService(
            HttpClient httpClient,
            AzureVaultService vaultService,
            IConfiguration configuration,
            ILogger<ApiService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _jwtSecret = vaultService.GetSecret("jwt-secret-key");
        }

        public async Task<string> GetJwtTokenAsync(CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiry)
            {
                return _cachedToken;
            }

            try
            {
                var apiBaseUrl = _configuration["ApiSettings:BaseUrl"];

                if (string.IsNullOrEmpty(apiBaseUrl))
                {
                    throw new InvalidOperationException("API Base URL not configured");
                }

                var tokenEndpoint = $"{apiBaseUrl.TrimEnd('/')}/token";
                var requestContent = new StringContent(
                    JsonSerializer.Serialize(new { username = "test", password = "pass" }),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync(tokenEndpoint, requestContent, cancellationToken);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (string.IsNullOrEmpty(tokenResponse?.Access_Token))
                {
                    throw new InvalidOperationException("Failed to retrieve access_token from API");
                }

                _cachedToken = tokenResponse.Access_Token;
                _tokenExpiry = DateTime.UtcNow.AddHours(1); // Default 1 hour expiry since API doesn't return expiry

                return _cachedToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve JWT token");
                throw;
            }
        }

        public async Task<TranslateResponse> TranslateAsync(TranslateRequest request, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                var token = await GetJwtTokenAsync(cancellationToken);
                var apiBaseUrl = _configuration["ApiSettings:BaseUrl"];

                if (string.IsNullOrEmpty(apiBaseUrl))
                {
                    throw new InvalidOperationException("API Base URL not configured");
                }

                var translateEndpoint = $"{apiBaseUrl.TrimEnd('/')}/translation/translateScript";

                var requestJson = JsonSerializer.Serialize(new
                {
                    blob_path = request.BlobPath,
                    glossary = request.Glossary
                }, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.PostAsync(translateEndpoint, requestContent, cancellationToken);

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Translation API returned error: {StatusCode} - {Content}",
                        response.StatusCode, responseContent);

                    return new TranslateResponse
                    {
                        Status = "error",
                        Message = $"API Error: {response.StatusCode} - {responseContent}"
                    };
                }

                var translationResponse = JsonSerializer.Deserialize<TranslateResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return translationResponse ?? new TranslateResponse
                {
                    Status = "error",
                    Message = "Failed to deserialize translation response"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to translate content for blob path: {BlobPath}", request.BlobPath);
                return new TranslateResponse
                {
                    Status = "error",
                    Message = ex.Message
                };
            }
        }

        public async Task<SummaryResponse> SummarizeAsync(string blobPath, CancellationToken cancellationToken = default)
        {
            try
            {
                var token = await GetJwtTokenAsync(cancellationToken);
                var apiBaseUrl = _configuration["ApiSettings:BaseUrl"];

                if (string.IsNullOrEmpty(apiBaseUrl))
                {
                    throw new InvalidOperationException("API Base URL not configured");
                }

                var summarizeEndpoint = $"{apiBaseUrl.TrimEnd('/')}/translation/summarizeScript";

                var requestJson = JsonSerializer.Serialize(new
                {
                    blob_path = blobPath
                }, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.PostAsync(summarizeEndpoint, requestContent, cancellationToken);

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Summary API returned error: {StatusCode} - {Content}",
                        response.StatusCode, responseContent);

                    return new SummaryResponse
                    {
                        Status = "error",
                        Message = $"API Error: {response.StatusCode} - {responseContent}"
                    };
                }

                var summaryResponse = JsonSerializer.Deserialize<SummaryResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return summaryResponse ?? new SummaryResponse
                {
                    Status = "error",
                    Message = "Failed to deserialize summary response"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to summarize content for blob path: {BlobPath}", blobPath);
                return new SummaryResponse
                {
                    Status = "error",
                    Message = ex.Message
                };
            }
        }

        public async Task<TranslateResponse> TranslateChainAsync(TranslateChainRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var token = await GetJwtTokenAsync(cancellationToken);
                var apiBaseUrl = _configuration["ApiSettings:BaseUrl"];

                if (string.IsNullOrEmpty(apiBaseUrl))
                {
                    throw new InvalidOperationException("API Base URL not configured");
                }

                var translateChainEndpoint = $"{apiBaseUrl.TrimEnd('/')}/translation/TranslateChain";

                var requestJson = JsonSerializer.Serialize(new
                {
                    container_name = request.ContainerName,
                    blob_path = request.BlobPath,
                    input_language = request.InputLanguage,
                    output_language = request.OutputLanguage,
                    glossary = request.Glossary
                });

                var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.PostAsync(translateChainEndpoint, requestContent, cancellationToken);

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("TranslateChain API returned error: {StatusCode} - {Content}",
                        response.StatusCode, responseContent);

                    return new TranslateResponse
                    {
                        Status = "error",
                        Message = $"API Error: {response.StatusCode} - {responseContent}"
                    };
                }

                var translationResponse = JsonSerializer.Deserialize<TranslateResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return translationResponse ?? new TranslateResponse
                {
                    Status = "error",
                    Message = "Failed to deserialize translation chain response"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to translate chain for blob path: {BlobPath}", request.BlobPath);
                return new TranslateResponse
                {
                    Status = "error",
                    Message = ex.Message
                };
            }
        }
    }
}