using Microsoft.AspNetCore.Http;

namespace RestaurantPos.Api.Services.Storage
{
    /// <summary>
    /// Provider-agnostic file storage. The local-disk implementation ships today;
    /// swapping it for Azure Blob / S3 / GCS later is a single DI registration
    /// change — no entity, repository or controller code is affected.
    ///
    /// Returned <see cref="StoredFileResult.RelativePath"/> is the canonical key
    /// persisted in the database. It MUST be opaque to callers — only the
    /// storage service knows how to resolve it back to bytes.
    /// </summary>
    public interface IFileStorageService
    {
        Task<StoredFileResult> SaveAsync(
            IFormFile file,
            string scope,
            CancellationToken ct = default);

        /// <summary>
        /// Opens a read-only stream for download. Caller MUST dispose. Throws
        /// <see cref="FileNotFoundException"/> when the underlying object is
        /// missing — the controller maps this to a 404.
        /// </summary>
        Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct = default);

        Task DeleteAsync(string relativePath, CancellationToken ct = default);

        /// <summary>
        /// Public absolute URL for direct browser rendering (image previews).
        /// Returns null in providers where files are not publicly addressable
        /// (e.g. private blob containers). Callers should fall back to the
        /// secure download endpoint when null is returned.
        /// </summary>
        string? GetPublicUrl(string relativePath);
    }

    public sealed class StoredFileResult
    {
        public string OriginalFileName { get; init; } = string.Empty;
        public string StoredFileName   { get; init; } = string.Empty;
        public string FileExtension    { get; init; } = string.Empty;
        public string MimeType         { get; init; } = string.Empty;
        public long   FileSize         { get; init; }
        public string RelativePath     { get; init; } = string.Empty;
        public string? PublicUrl       { get; init; }
    }
}
