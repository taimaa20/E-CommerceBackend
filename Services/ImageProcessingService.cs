using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace RestaurantPos.Api.Services
{
    /// <summary>
    /// Result of a successful image processing pass. The key is GUID-only
    /// (no extension, no path). Consumers derive variant URLs via the
    /// "{key}-{size}.webp" convention — matches the filenames written to
    /// wwwroot/uploads/images by <see cref="ImageProcessingService"/>.
    /// </summary>
    public sealed record ImageVariants(
        string Key,
        string ThumbFileName,
        string MediumFileName,
        string FullFileName);

    public interface IImageProcessingService
    {
        /// <summary>
        /// Decode the incoming image stream once, strip EXIF/orient,
        /// and emit three WebP variants (thumb/medium/full) into
        /// <paramref name="outputDirectory"/>. Returns the shared
        /// <see cref="ImageVariants.Key"/> so callers can persist it
        /// alongside the legacy ImageUrl and continue running both
        /// pipelines side-by-side.
        /// </summary>
        Task<ImageVariants> ProcessAndSaveAsync(
            Stream input,
            string outputDirectory,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Deterministic WebP pipeline. Runs entirely in-process on pure managed
    /// ImageSharp — no native dependencies and no per-RID binaries, which keeps
    /// Azure Linux deploys straightforward. One decode, three encodes; the
    /// original bytes are also preserved by <c>UploadsController</c> for
    /// backward compatibility with pre-pipeline URLs.
    /// </summary>
    public sealed class ImageProcessingService : IImageProcessingService
    {
        // Long-edge caps. Photos larger than these ship resampled; smaller
        // photos pass through untouched (still re-encoded for EXIF strip).
        private const int ThumbMaxEdge = 200;   // admin list, list-mode menu rows
        private const int MediumMaxEdge = 600;  // grid cards (POS & public menu)
        private const int FullMaxEdge = 1600;   // detail hero

        // WebP quality. Lossy across the board — visually lossless for food
        // photography at these dimensions, and 6-10× smaller than source JPEG.
        private const int ThumbQuality = 70;
        private const int MediumQuality = 75;
        private const int FullQuality = 80;

        public async Task<ImageVariants> ProcessAndSaveAsync(
            Stream input,
            string outputDirectory,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

            Directory.CreateDirectory(outputDirectory);

            // Decode ONCE. Orient from EXIF (so portrait phone photos aren't
            // sideways), then strip EXIF + ICC so we're not publishing GPS
            // coordinates or camera metadata to every customer device.
            using var source = await Image.LoadAsync(input, cancellationToken)
                .ConfigureAwait(false);
            source.Mutate(ctx => ctx.AutoOrient());
            source.Metadata.ExifProfile = null;
            source.Metadata.IccProfile = null;
            source.Metadata.IptcProfile = null;
            source.Metadata.XmpProfile = null;

            var key = Guid.NewGuid().ToString("N");
            var thumb = $"{key}-thumb.webp";
            var medium = $"{key}-medium.webp";
            var full = $"{key}-full.webp";

            await SaveVariantAsync(source, ThumbMaxEdge, ThumbQuality,
                Path.Combine(outputDirectory, thumb), cancellationToken);
            await SaveVariantAsync(source, MediumMaxEdge, MediumQuality,
                Path.Combine(outputDirectory, medium), cancellationToken);
            await SaveVariantAsync(source, FullMaxEdge, FullQuality,
                Path.Combine(outputDirectory, full), cancellationToken);

            return new ImageVariants(key, thumb, medium, full);
        }

        private static async Task SaveVariantAsync(
            Image source,
            int maxEdge,
            int quality,
            string outputPath,
            CancellationToken cancellationToken)
        {
            // Clone so each variant gets its own resize — Mutate on the source
            // would compound resizes and destroy the next variant's fidelity.
            using var copy = source.Clone(ctx =>
            {
                var width = source.Width;
                var height = source.Height;
                if (width > maxEdge || height > maxEdge)
                {
                    ctx.Resize(new ResizeOptions
                    {
                        Size = new Size(maxEdge, maxEdge),
                        Mode = ResizeMode.Max,           // fit within box, preserve aspect
                        Sampler = KnownResamplers.Lanczos3,
                    });
                }
            });

            var encoder = new WebpEncoder
            {
                Quality = quality,
                FileFormat = WebpFileFormatType.Lossy,
            };

            await using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await copy.SaveAsync(fs, encoder, cancellationToken).ConfigureAwait(false);
        }
    }
}
