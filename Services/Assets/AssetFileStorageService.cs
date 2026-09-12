using Microsoft.AspNetCore.Http;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services.Assets
{
    public sealed class AssetStorageOptions
    {
        public string RootFolder { get; set; } = "uploads/assets";
        public string ImagesFolder { get; set; } = "images";
        public string DocumentsFolder { get; set; } = "documents";
        public string WarrantyFolder { get; set; } = "warranty";
        public string MaintenanceFolder { get; set; } = "maintenance";
        public long MaxFileSizeBytes { get; set; } = AssetFileStorageService.DefaultMaxFileSizeBytes;
    }

    public interface IAssetFileStorageService
    {
        Task<AssetStoredFileResult> SaveAsync(IFormFile file, AssetAttachmentType attachmentType, CancellationToken ct);
        Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct);
        Task DeleteAsync(string relativePath, CancellationToken ct);
        string? GetPublicUrl(string relativePath);
    }

    public sealed class AssetStoredFileResult
    {
        public string OriginalFileName { get; init; } = string.Empty;
        public string StoredFileName { get; init; } = string.Empty;
        public string FileExtension { get; init; } = string.Empty;
        public string MimeType { get; init; } = string.Empty;
        public long FileSize { get; init; }
        public string RelativePath { get; init; } = string.Empty;
        public string? PublicUrl { get; init; }
    }

    public sealed class AssetFileStorageService : IAssetFileStorageService
    {
        public const long DefaultMaxFileSizeBytes = 100 * 1024 * 1024;

        private static readonly Dictionary<string, string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/webp"] = ".webp",
            ["application/pdf"] = ".pdf",
            ["application/msword"] = ".doc",
            ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = ".docx",
            ["application/vnd.ms-excel"] = ".xls",
            ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = ".xlsx",
            ["text/plain"] = ".txt",
            ["video/mp4"] = ".mp4",
            ["video/quicktime"] = ".mov",
            ["video/webm"] = ".webm"
        };

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".mp4", ".mov", ".webm"
        };

        private readonly IWebHostEnvironment _env;
        private readonly IHttpContextAccessor _httpContext;
        private readonly AssetStorageOptions _options;
        private readonly ILogger<AssetFileStorageService> _logger;

        public AssetFileStorageService(
            IWebHostEnvironment env,
            IHttpContextAccessor httpContext,
            Microsoft.Extensions.Options.IOptions<AssetStorageOptions> options,
            ILogger<AssetFileStorageService> logger)
        {
            _env = env ?? throw new ArgumentNullException(nameof(env));
            _httpContext = httpContext ?? throw new ArgumentNullException(nameof(httpContext));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<AssetStoredFileResult> SaveAsync(IFormFile file, AssetAttachmentType attachmentType, CancellationToken ct)
        {
            if (file is null || file.Length == 0)
                throw new ValidationException("No file provided.");

            var maxBytes = _options.MaxFileSizeBytes <= 0 ? DefaultMaxFileSizeBytes : _options.MaxFileSizeBytes;
            if (file.Length > maxBytes)
                throw new ValidationException($"File size exceeds the {maxBytes / (1024 * 1024)} MB limit.");

            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? string.Empty;
            if (!AllowedExtensions.Contains(ext))
                throw new ValidationException("Invalid file type for asset attachment.");

            var mime = (file.ContentType ?? string.Empty).ToLowerInvariant();
            if (!AllowedMimeTypes.TryGetValue(mime, out var canonicalExt))
                throw new ValidationException("Invalid MIME type for asset attachment.");

            if (ext == ".jpeg") ext = ".jpg";
            if (!string.Equals(ext, canonicalExt, StringComparison.OrdinalIgnoreCase) && canonicalExt != ".jpg")
                throw new ValidationException("File extension does not match the uploaded MIME type.");

            var now = DateTime.UtcNow;
            var subFolder = ResolveSubFolder(attachmentType, mime);
            var relativeFolder = Path.Combine(
                SanitiseFolder(_options.RootFolder),
                subFolder,
                now.Year.ToString("D4"),
                now.Month.ToString("D2"));
            var absoluteFolder = Path.Combine(WebRootPath(), relativeFolder);
            Directory.CreateDirectory(absoluteFolder);

            var storedFileName = $"{Guid.NewGuid():N}{ext}";
            var absolutePath = Path.Combine(absoluteFolder, storedFileName);

            await using (var fs = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await file.CopyToAsync(fs, ct);
            }

            var relativePath = $"{relativeFolder.Replace('\\', '/')}/{storedFileName}";
            _logger.LogInformation("Saved asset attachment {RelativePath} ({Bytes} bytes)", relativePath, file.Length);

            return new AssetStoredFileResult
            {
                OriginalFileName = SanitiseOriginalName(file.FileName),
                StoredFileName = storedFileName,
                FileExtension = ext,
                MimeType = mime,
                FileSize = file.Length,
                RelativePath = relativePath,
                PublicUrl = GetPublicUrl(relativePath)
            };
        }

        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct)
        {
            var absolute = ResolveAbsolutePath(relativePath);
            if (!File.Exists(absolute))
                throw new FileNotFoundException("Asset attachment file is missing.", relativePath);

            Stream stream = new FileStream(
                absolute,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81_920,
                useAsync: true);
            return Task.FromResult(stream);
        }

        public Task DeleteAsync(string relativePath, CancellationToken ct)
        {
            try
            {
                var absolute = ResolveAbsolutePath(relativePath);
                if (File.Exists(absolute))
                    File.Delete(absolute);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete asset attachment file {RelativePath}", relativePath);
            }

            return Task.CompletedTask;
        }

        public string? GetPublicUrl(string relativePath)
        {
            var request = _httpContext.HttpContext?.Request;
            if (request is null) return null;
            return $"{request.Scheme}://{request.Host}/{relativePath.TrimStart('/')}";
        }

        private string ResolveSubFolder(AssetAttachmentType attachmentType, string mimeType)
            => attachmentType switch
            {
                AssetAttachmentType.Photo => SanitiseSegment(_options.ImagesFolder),
                AssetAttachmentType.WarrantyDocument => SanitiseSegment(_options.WarrantyFolder),
                AssetAttachmentType.MaintenanceReport => SanitiseSegment(_options.MaintenanceFolder),
                _ when mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) => SanitiseSegment(_options.ImagesFolder),
                _ => SanitiseSegment(_options.DocumentsFolder)
            };

        private string WebRootPath()
            => string.IsNullOrEmpty(_env.WebRootPath)
                ? Path.Combine(_env.ContentRootPath, "wwwroot")
                : _env.WebRootPath;

        private string ResolveAbsolutePath(string relativePath)
        {
            var root = Path.GetFullPath(WebRootPath());
            var candidate = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new ValidationException("Invalid asset attachment path.");

            return candidate;
        }

        private static string SanitiseFolder(string folder)
        {
            var parts = folder.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(SanitiseSegment)
                .Where(p => p.Length > 0);
            var clean = Path.Combine(parts.ToArray());
            return string.IsNullOrWhiteSpace(clean) ? Path.Combine("uploads", "assets") : clean;
        }

        private static string SanitiseSegment(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "misc";
            var clean = new string(raw.Trim().ToLowerInvariant()
                .Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '-')
                .ToArray());
            return string.IsNullOrWhiteSpace(clean) ? "misc" : clean;
        }

        private static string SanitiseOriginalName(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "file";
            var nameOnly = Path.GetFileName(raw);
            var invalid = Path.GetInvalidFileNameChars();
            var clean = new string(nameOnly.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            return clean.Length > 260 ? clean[..260] : clean;
        }
    }
}
