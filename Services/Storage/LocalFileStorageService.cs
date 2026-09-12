using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using RestaurantPos.Api.Exceptions;

namespace RestaurantPos.Api.Services.Storage
{
    /// <summary>
    /// Local-disk implementation. Files land under
    /// <c>wwwroot/uploads/{scope}/{yyyy}/{MM}/{guid}{ext}</c> so the year/month
    /// bucket layout works for both hot files and an eventual archive sweep.
    ///
    /// Security model:
    ///   • Extension allow-list (jpg/jpeg/png/webp/pdf only).
    ///   • MIME allow-list — defence-in-depth in case the browser lies about ext.
    ///   • File size capped at <see cref="MaxFileSize"/>.
    ///   • Stored file name is a freshly generated GUID — the original is NEVER
    ///     used on disk. Path traversal via "../" is impossible because we
    ///     compose the path ourselves from primitive parts.
    /// </summary>
    public sealed class LocalFileStorageService : IFileStorageService
    {
        // 10 MB — sized for restaurant receipts; PDF supplier invoices rarely exceed this.
        public const long MaxFileSize = 10 * 1024 * 1024;

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".pdf"
        };

        private static readonly Dictionary<string, string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"]       = "image/jpeg",
            ["image/png"]        = "image/png",
            ["image/webp"]       = "image/webp",
            ["application/pdf"]  = "application/pdf"
        };

        private readonly IWebHostEnvironment _env;
        private readonly IHttpContextAccessor _httpContext;
        private readonly ILogger<LocalFileStorageService> _logger;

        public LocalFileStorageService(
            IWebHostEnvironment env,
            IHttpContextAccessor httpContext,
            ILogger<LocalFileStorageService> logger)
        {
            _env = env ?? throw new ArgumentNullException(nameof(env));
            _httpContext = httpContext ?? throw new ArgumentNullException(nameof(httpContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<StoredFileResult> SaveAsync(IFormFile file, string scope, CancellationToken ct = default)
        {
            if (file is null || file.Length == 0)
                throw new ValidationException("No file provided.");

            if (file.Length > MaxFileSize)
                throw new ValidationException($"File size exceeds the {MaxFileSize / (1024 * 1024)} MB limit.");

            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? string.Empty;
            if (!AllowedExtensions.Contains(ext))
                throw new ValidationException("Invalid file type. Allowed: jpg, jpeg, png, webp, pdf.");

            var mime = (file.ContentType ?? string.Empty).ToLowerInvariant();
            if (!AllowedMimeTypes.TryGetValue(mime, out var safeMime))
                throw new ValidationException("Invalid MIME type.");

            // Bucket by year/month so a single folder never grows unbounded.
            var now = DateTime.UtcNow;
            var safeScope = SanitiseScope(scope);
            var relativeFolder = Path.Combine("uploads", safeScope, now.Year.ToString("D4"), now.Month.ToString("D2"));
            var absoluteFolder = Path.Combine(WebRootPath(), relativeFolder);
            Directory.CreateDirectory(absoluteFolder);

            var storedFileName = $"{Guid.NewGuid():N}{ext}";
            var absolutePath = Path.Combine(absoluteFolder, storedFileName);

            await using (var fs = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await file.CopyToAsync(fs, ct);
            }

            // Always use forward slashes — RelativePath flows into URLs untouched.
            var relativePath = $"{relativeFolder.Replace('\\', '/')}/{storedFileName}";
            var publicUrl = GetPublicUrl(relativePath);

            _logger.LogInformation("Saved expense-invoice attachment {RelativePath} ({Bytes} bytes)", relativePath, file.Length);

            return new StoredFileResult
            {
                OriginalFileName = SanitiseOriginalName(file.FileName),
                StoredFileName   = storedFileName,
                FileExtension    = ext,
                MimeType         = safeMime,
                FileSize         = file.Length,
                RelativePath     = relativePath,
                PublicUrl        = publicUrl
            };
        }

        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct = default)
        {
            var absolute = ResolveAbsolutePath(relativePath);

            if (!File.Exists(absolute))
                throw new FileNotFoundException("Attachment file is missing on disk.", relativePath);

            // FileShare.Read so concurrent downloads don't fight each other.
            Stream stream = new FileStream(
                absolute,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81_920,
                useAsync: true);

            return Task.FromResult(stream);
        }

        public Task DeleteAsync(string relativePath, CancellationToken ct = default)
        {
            try
            {
                var absolute = ResolveAbsolutePath(relativePath);
                if (File.Exists(absolute))
                    File.Delete(absolute);
            }
            catch (Exception ex)
            {
                // Don't surface storage cleanup failures to the user — the DB
                // row is already gone. Log and let an admin sweeper reconcile.
                _logger.LogWarning(ex, "Failed to delete attachment file {RelativePath}", relativePath);
            }
            return Task.CompletedTask;
        }

        public string? GetPublicUrl(string relativePath)
        {
            var request = _httpContext.HttpContext?.Request;
            if (request is null)
                return null;

            return $"{request.Scheme}://{request.Host}/{relativePath.TrimStart('/')}";
        }

        // ────────────────────────── helpers ────────────────────────────────

        private string WebRootPath()
            => string.IsNullOrEmpty(_env.WebRootPath)
                ? Path.Combine(_env.ContentRootPath, "wwwroot")
                : _env.WebRootPath;

        // Resolves the canonical absolute path AND verifies it stays inside
        // the configured wwwroot — defence against any malicious RelativePath
        // a future caller might construct.
        private string ResolveAbsolutePath(string relativePath)
        {
            var root = Path.GetFullPath(WebRootPath());
            var candidate = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

            if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new ValidationException("Invalid attachment path.");

            return candidate;
        }

        private static string SanitiseScope(string scope)
        {
            if (string.IsNullOrWhiteSpace(scope))
                return "misc";

            // Reject anything that isn't a-z, 0-9, dash or underscore.
            var clean = new string(scope.Trim().ToLowerInvariant()
                .Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '-')
                .ToArray());
            return string.IsNullOrEmpty(clean) ? "misc" : clean;
        }

        private static string SanitiseOriginalName(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "file";

            // Strip any path component a client may have included.
            var nameOnly = Path.GetFileName(raw);
            var invalid = Path.GetInvalidFileNameChars();
            var clean = new string(nameOnly.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            return clean.Length > 260 ? clean[..260] : clean;
        }
    }
}
