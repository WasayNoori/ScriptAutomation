namespace ScriptProcessor.Services
{
    public interface IFileService
    {
        Task<UploadResult> UploadAsync(
            Guid scriptId,
            string artifactName,              // no container, no prefix
            Stream content,
            string contentType,
            int? languageId = null,           // optional tags
            string? derivedFrom = null,       // optional lineage
            CancellationToken ct = default);

        Task<UploadResult> UploadFromFileAsync(
            Guid scriptId,
            string artifactName,
            string filePath,
            string contentType,
            int? languageId = null,
            string? derivedFrom = null,
            CancellationToken ct = default);

        Task<Stream> DownloadAsync(string blobName, CancellationToken ct = default);
        Task DeleteAsync(string blobName, CancellationToken ct = default);
        Task<IEnumerable<BlobFileInfo>> ListFilesAsync(CancellationToken ct = default);
        Task<BlobFileDetails> GetFileDetailsAsync(string blobName, CancellationToken ct = default);
        Task<string> GetBlobUrlAsync(string blobName, CancellationToken ct = default);
        Task<UploadResult> SaveBlobAsync(string blobName, string textContent, CancellationToken ct = default);
        Task<UploadResult> CreateRelatedFileAsync(string originalBlobName, string newSuffix, string textContent, CancellationToken ct = default);
    }

    public record UploadResult(string BlobName, string? VersionId, string ETag, long Size);
    
    public record BlobFileInfo(
        string BlobName, 
        string FileName, 
        long Size, 
        DateTimeOffset LastModified, 
        string? ScriptId, 
        string? LanguageId);
    
    public record BlobFileDetails(
        string BlobName,
        string FileName,
        long Size,
        DateTimeOffset LastModified,
        string ContentType,
        Dictionary<string, string> Metadata,
        string TextContent,
        string? SummaryContent = null,
        string? TranslatedContent = null);

}
