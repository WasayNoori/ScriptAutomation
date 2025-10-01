using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace ScriptProcessor.Services
{
    public class AzureBlobService : IFileService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _containerName;
        private readonly ILogger<AzureBlobService> _logger;

        public AzureBlobService(BlobServiceClient blobServiceClient, IConfiguration configuration, ILogger<AzureBlobService> logger)
        {
            _blobServiceClient = blobServiceClient;
            _logger = logger;
            _containerName = configuration["Azure:BlobStorage:ContainerName"] ?? "scripts";
        }

        public async Task<UploadResult> UploadAsync(
            Guid scriptId, 
            string artifactName, 
            Stream content, 
            string contentType, 
            int? languageId = null, 
            string? derivedFrom = null, 
            CancellationToken ct = default)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);

            var blobName = $"scripts/{scriptId}/{artifactName}";
            var blobClient = containerClient.GetBlobClient(blobName);

            var metadata = new Dictionary<string, string>
            {
                ["scriptId"] = scriptId.ToString(),
                ["artifactName"] = artifactName,
                ["contentType"] = contentType,
                ["uploadedAt"] = DateTimeOffset.UtcNow.ToString("O")
            };

            if (languageId.HasValue)
                metadata["languageId"] = languageId.Value.ToString();

            if (!string.IsNullOrEmpty(derivedFrom))
                metadata["derivedFrom"] = derivedFrom;

            var options = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
                Metadata = metadata,
                Conditions = new BlobRequestConditions()
            };

            var response = await blobClient.UploadAsync(content, options, ct);
            
            _logger.LogInformation("Uploaded blob {BlobName} for script {ScriptId}", blobName, scriptId);

            return new UploadResult(
                blobName,
                response.Value.VersionId,
                response.Value.ETag.ToString(),
                content.Length);
        }

        public async Task<UploadResult> UploadFromFileAsync(
            Guid scriptId, 
            string artifactName, 
            string filePath, 
            string contentType, 
            int? languageId = null, 
            string? derivedFrom = null, 
            CancellationToken ct = default)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            
            _logger.LogInformation("Uploading file {FilePath} as blob for script {ScriptId}", filePath, scriptId);
            
            return await UploadAsync(scriptId, artifactName, fileStream, contentType, languageId, derivedFrom, ct);
        }

        public async Task<Stream> DownloadAsync(string blobName, CancellationToken ct = default)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            if (!await blobClient.ExistsAsync(ct))
            {
                throw new FileNotFoundException($"Blob '{blobName}' not found in container '{_containerName}'");
            }

            var response = await blobClient.DownloadStreamingAsync(cancellationToken: ct);
            _logger.LogInformation("Downloaded blob {BlobName}", blobName);
            
            return response.Value.Content;
        }

        public async Task DeleteAsync(string blobName, CancellationToken ct = default)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            var response = await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct);
            
            if (response.Value)
            {
                _logger.LogInformation("Deleted blob {BlobName}", blobName);
            }
            else
            {
                _logger.LogWarning("Blob {BlobName} not found for deletion", blobName);
            }
        }

        public async Task<IEnumerable<BlobFileInfo>> ListFilesAsync(CancellationToken ct = default)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobFiles = new List<BlobFileInfo>();

            await foreach (var blobItem in containerClient.GetBlobsAsync(traits: Azure.Storage.Blobs.Models.BlobTraits.Metadata, cancellationToken: ct))
            {
                var fileName = Path.GetFileName(blobItem.Name);
                
                blobItem.Metadata.TryGetValue("scriptId", out var scriptId);
                blobItem.Metadata.TryGetValue("languageId", out var languageId);

                blobFiles.Add(new BlobFileInfo(
                    blobItem.Name,
                    fileName,
                    blobItem.Properties.ContentLength ?? 0,
                    blobItem.Properties.LastModified ?? DateTimeOffset.MinValue,
                    scriptId,
                    languageId));
            }

            _logger.LogInformation("Listed {Count} files from container {ContainerName}", blobFiles.Count, _containerName);
            return blobFiles.OrderByDescending(f => f.LastModified);
        }

        public async Task<BlobFileDetails> GetFileDetailsAsync(string blobName, CancellationToken ct = default)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            if (!await blobClient.ExistsAsync(ct))
            {
                throw new FileNotFoundException($"Blob '{blobName}' not found in container '{_containerName}'");
            }

            // Get properties and metadata
            var properties = await blobClient.GetPropertiesAsync(cancellationToken: ct);
            
            // Download content as text
            var downloadResponse = await blobClient.DownloadContentAsync(ct);
            var textContent = downloadResponse.Value.Content.ToString();

            var fileName = Path.GetFileName(blobName);

            _logger.LogInformation("Retrieved details for blob {BlobName}", blobName);

            return new BlobFileDetails(
                blobName,
                fileName,
                properties.Value.ContentLength,
                properties.Value.LastModified,
                properties.Value.ContentType ?? "text/plain",
                properties.Value.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                textContent,
                null, // SummaryContent - will be populated later
                null  // TranslatedContent - will be populated from API response
            );
        }

        public async Task<string> GetBlobUrlAsync(string blobName, CancellationToken ct = default)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            if (!await blobClient.ExistsAsync(ct))
            {
                throw new FileNotFoundException($"Blob '{blobName}' not found in container '{_containerName}'");
            }

            // Return the full URL to the blob
            return blobClient.Uri.ToString();
        }
    }
}
