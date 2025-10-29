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

        public async Task<UploadResult> SaveBlobAsync(string blobName, string textContent, CancellationToken ct = default)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);

            var blobClient = containerClient.GetBlobClient(blobName);

            // Convert string content to stream
            using var contentStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(textContent));

            // Get existing metadata if blob exists
            var metadata = new Dictionary<string, string>();
            try
            {
                var existingProperties = await blobClient.GetPropertiesAsync(cancellationToken: ct);
                metadata = existingProperties.Value.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                metadata["lastUpdated"] = DateTimeOffset.UtcNow.ToString("O");
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                // Blob doesn't exist, create new metadata
                metadata["createdAt"] = DateTimeOffset.UtcNow.ToString("O");
                metadata["contentType"] = "text/plain";
            }

            var options = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "text/plain" },
                Metadata = metadata
            };

            // Delete existing blob if it exists to ensure we can overwrite
            await blobClient.DeleteIfExistsAsync(cancellationToken: ct);

            var uploadResponse = await blobClient.UploadAsync(contentStream, options, cancellationToken: ct);

            _logger.LogInformation("Successfully saved blob {BlobName} with {Size} bytes",
                blobName, textContent.Length);

            return new UploadResult(blobName, uploadResponse.Value.VersionId, uploadResponse.Value.ETag.ToString(), textContent.Length);
        }

        public async Task<UploadResult> CreateRelatedFileAsync(string originalBlobName, string newSuffix, string textContent, CancellationToken ct = default)
        {
            // Parse the original blob name to create the new name
            var pathParts = originalBlobName.Split('/');
            var fileName = pathParts.Last();
            var directory = string.Join("/", pathParts.Take(pathParts.Length - 1));

            // Remove extension and add suffix
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            var newFileName = $"{fileNameWithoutExtension}-{newSuffix}.txt";
            var newBlobName = directory.Length > 0 ? $"{directory}/{newFileName}" : newFileName;

            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);

            var blobClient = containerClient.GetBlobClient(newBlobName);

            // Convert string content to stream
            using var contentStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(textContent));

            // Get metadata from original blob if it exists
            var metadata = new Dictionary<string, string>();
            try
            {
                var originalBlobClient = containerClient.GetBlobClient(originalBlobName);
                var originalProperties = await originalBlobClient.GetPropertiesAsync(cancellationToken: ct);

                // Copy relevant metadata from original
                metadata = originalProperties.Value.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                metadata["derivedFrom"] = originalBlobName;
                metadata["suffix"] = newSuffix;
                metadata["createdAt"] = DateTimeOffset.UtcNow.ToString("O");
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                // Original blob not found, create basic metadata
                metadata["contentType"] = "text/plain";
                metadata["suffix"] = newSuffix;
                metadata["createdAt"] = DateTimeOffset.UtcNow.ToString("O");
                _logger.LogWarning("Original blob {OriginalBlobName} not found, creating related file without original metadata", originalBlobName);
            }

            var options = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "text/plain" },
                Metadata = metadata
            };

            // Delete existing blob if it exists to ensure we can overwrite
            await blobClient.DeleteIfExistsAsync(cancellationToken: ct);

            var uploadResponse = await blobClient.UploadAsync(contentStream, options, cancellationToken: ct);

            _logger.LogInformation("Successfully created related file {NewBlobName} from {OriginalBlobName} with suffix {Suffix}",
                newBlobName, originalBlobName, newSuffix);

            return new UploadResult(newBlobName, uploadResponse.Value.VersionId, uploadResponse.Value.ETag.ToString(), textContent.Length);
        }
    }
}
