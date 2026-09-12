using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class UploadsController : ControllerBase
    {
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp"
        };

        private static readonly Dictionary<string, string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = "image/jpeg",
            ["image/png"]  = "image/png",
            ["image/gif"]  = "image/gif",
            ["image/webp"] = "image/webp"
        };

        private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB

        private readonly IImageProcessingService _imageProcessing;
        private readonly ILogger<UploadsController> _logger;

        public UploadsController(
            IImageProcessingService imageProcessing,
            ILogger<UploadsController> logger)
        {
            _imageProcessing = imageProcessing ?? throw new ArgumentNullException(nameof(imageProcessing));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // POST: api/uploads/image
        // Stores the uploaded file under wwwroot/uploads/images and returns a public URL.
        //
        // Behaviour (unchanged for existing callers):
        //   - `url`, `contentType`, `fileName` remain in the response body with the
        //     same meaning — legacy admin form reads `url` and writes it to
        //     Product.ImageUrl. Nothing breaks for clients that ignore new fields.
        //
        // Additive enhancements (new callers opt in by reading `imageKey`):
        //   - Also produces three WebP variants (thumb/medium/full) alongside the
        //     original via IImageProcessingService. Variant files share a GUID
        //     stem: {key}-thumb.webp / {key}-medium.webp / {key}-full.webp.
        //   - Returns `imageKey` + a `variants` map so the frontend can persist
        //     the key and render responsive <img srcSet>. Legacy originals continue
        //     to work: the frontend helper falls back to `imageUrl` when
        //     `imageKey` is absent on older rows.
        [HttpPost("image")]
        public async Task<IActionResult> UploadImage(IFormFile file, CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file provided." });

            if (file.Length > MaxFileSize)
                return BadRequest(new { message = "File size exceeds the 5 MB limit." });

            var ext = Path.GetExtension(file.FileName);
            if (!AllowedExtensions.Contains(ext))
                return BadRequest(new { message = "Invalid file type. Allowed: jpg, jpeg, png, gif, webp." });

            var contentType = file.ContentType.ToLowerInvariant();
            if (!AllowedMimeTypes.TryGetValue(contentType, out var safeMime))
                return BadRequest(new { message = "Invalid MIME type." });

            var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "images");
            Directory.CreateDirectory(uploadsRoot);

            // 1) Preserve the original file exactly as before — same naming scheme,
            //    same URL shape. Any existing client that only reads `url` keeps
            //    working without changes.
            var safeFileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
            var originalPath = Path.Combine(uploadsRoot, safeFileName);

            await using (var fs = new FileStream(originalPath, FileMode.Create))
            {
                await file.CopyToAsync(fs, cancellationToken);
            }

            var publicUrl = $"{Request.Scheme}://{Request.Host}/uploads/images/{safeFileName}";

            // 2) Produce thumb/medium/full WebP variants. Best-effort: a failure
            //    here must NOT break the upload — the original on disk is still
            //    serviceable through the legacy `url` path. We surface the
            //    failure in logs and return without an `imageKey`, and the
            //    frontend helper will fall back to the legacy URL automatically.
            string? imageKey = null;
            object? variants = null;
            try
            {
                await using var reopened = new FileStream(
                    originalPath, FileMode.Open, FileAccess.Read, FileShare.Read);

                var result = await _imageProcessing.ProcessAndSaveAsync(
                    reopened, uploadsRoot, cancellationToken);

                imageKey = result.Key;
                var baseUrl = $"{Request.Scheme}://{Request.Host}/uploads/images";
                variants = new
                {
                    thumb = $"{baseUrl}/{result.ThumbFileName}",
                    medium = $"{baseUrl}/{result.MediumFileName}",
                    full = $"{baseUrl}/{result.FullFileName}",
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Image variant pipeline failed for {FileName}. Falling back to original-only upload.",
                    safeFileName);
            }

            return Ok(new
            {
                url = publicUrl,
                contentType = safeMime,
                fileName = safeFileName,
                // New additive fields — safe for existing clients to ignore.
                imageKey,
                variants,
            });
        }
    }
}
