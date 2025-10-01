namespace ScriptProcessor.Services
{
    public interface IApiService
    {
        Task<string> GetJwtTokenAsync(CancellationToken cancellationToken = default);
        Task<TranslateResponse> TranslateAsync(TranslateRequest request, CancellationToken cancellationToken = default);
    }

    public class TranslateRequest
    {
        public string BlobPath { get; set; } = string.Empty;
        public Dictionary<string, string> Glossary { get; set; } = new();
    }

    public class TranslateResponse
    {
        public string Status { get; set; } = string.Empty;
        public TranslationResult? Translation_Result { get; set; }

        // Computed properties for backward compatibility
        public bool Success => Status == "success" && Translation_Result != null;
        public string? Message { get; set; }
        public string? TranslatedContent => Translation_Result?.Translated_Text;
    }

    public class TranslationResult
    {
        public string? Translated_Text { get; set; }
        public int? Wordcount { get; set; }
    }

    public class TokenResponse
    {
        public string Access_Token { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }
}