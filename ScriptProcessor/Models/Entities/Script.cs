namespace ScriptProcessor.Models.Entities
{
    public sealed class Script
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public Guid? LessonId { get; set; }
        public int LanguageId { get; set; }
        public string? Description { get; set; }
        public string Author { get; set; } = null!;

        // Blob reference within a single configured container
        public string BlobName { get; set; } = null!;        // e.g., "scripts/{Id}/original.txt" or "script-A.fp1.txt"
        public string? BlobVersionId { get; set; }           // if using versioning
        public string? BlobETag { get; set; }                // optional for concurrency

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? UpdatedAt { get; set; }
    }

}
