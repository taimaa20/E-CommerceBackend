using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Middleware
{
    public class IdempotencyMiddleware
    {
        private const string HeaderName = "X-Idempotency-Key";
        private static readonly TimeSpan ProcessingTtl = TimeSpan.FromMinutes(10);

        private static readonly HashSet<string> ScopedMethods = new(StringComparer.OrdinalIgnoreCase)
        {
            HttpMethods.Post,
            HttpMethods.Patch,
            HttpMethods.Put,
        };

        private static readonly string[] ScopedPathPrefixes =
        {
            "/api/Orders",
            "/api/Payments",
            "/api/cashierbalance",
            "/api/Printing",
        };

        private readonly RequestDelegate _next;
        private readonly IServiceScopeFactory _scopeFactory;

        public IdempotencyMiddleware(
            RequestDelegate next,
            IServiceScopeFactory scopeFactory)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        }

        public async Task InvokeAsync(
            HttpContext context,
            ITenantResolver tenantResolver,
            ILogger<IdempotencyMiddleware> logger)
        {
            if (!ShouldHandle(context) || !TryGetKey(context, out var key))
            {
                await _next(context);
                return;
            }

            var tenantId = tenantResolver.GetTenantId();
            var requestHash = await ComputeRequestHashAsync(context);
            var reservation = await ReserveAsync(context, tenantId, key, requestHash, logger);

            if (reservation.Kind == ReservationKind.Replay)
            {
                await ReplayAsync(context, reservation.Entry!);
                return;
            }

            if (reservation.Kind == ReservationKind.InFlight)
            {
                await WriteConflictAsync(context, "Request already in flight");
                return;
            }

            if (reservation.Kind == ReservationKind.Mismatch)
            {
                await WriteConflictAsync(context, "Idempotency key was reused with a different request");
                return;
            }

            await CaptureAndStoreResponseAsync(context, reservation.Entry!, logger);
        }

        private async Task CaptureAndStoreResponseAsync(
            HttpContext context,
            IdempotencyEntry entry,
            ILogger<IdempotencyMiddleware> logger)
        {
            var originalBody = context.Response.Body;
            await using var buffer = new MemoryStream();
            context.Response.Body = buffer;

            var handlerSucceeded = false;
            try
            {
                await _next(context);
                handlerSucceeded = true;

                buffer.Position = 0;
                var body = await new StreamReader(buffer, Encoding.UTF8).ReadToEndAsync();
                buffer.Position = 0;

                // P2 (production fix): COMMIT the idempotency record BEFORE
                // we attempt to write the response to the client. Previously
                // we copied the response first, then marked Completed —
                // which meant any failure during the network write (client
                // disconnect, TLS reset, mid-response timeout) sent us into
                // the catch block, removed the entry, and a retry minutes
                // later landed on an empty store → DUPLICATE payment / item
                // / stock movement / shift open. Persisting first means a
                // committed handler stays committed in the idempotency log
                // no matter what happens on the wire afterwards; the worst
                // case is the client retries and receives the replayed
                // response, which is the contract we want.
                if (context.Response.StatusCode < StatusCodes.Status500InternalServerError)
                {
                    await CompleteAsync(entry.Id, context.Response.StatusCode, context.Response.ContentType, body);
                    logger.LogInformation("Idempotency stored response for key {Key}", entry.Key);
                }
                else
                {
                    await RemoveAsync(entry.Id);
                }

                // Now safe to forward the bytes to the client. If this
                // throws (client disconnect), the catch block will see
                // handlerSucceeded=true and will NOT remove the entry.
                await buffer.CopyToAsync(originalBody, context.RequestAborted);
            }
            catch
            {
                // Only remove on TRUE handler failure. Once the handler has
                // committed and the idempotency entry is recorded, we keep
                // the record so retries replay instead of re-executing.
                if (!handlerSucceeded)
                {
                    await RemoveAsync(entry.Id);
                }
                throw;
            }
            finally
            {
                context.Response.Body = originalBody;
            }
        }

        private async Task<Reservation> ReserveAsync(
            HttpContext context,
            Guid tenantId,
            string key,
            string requestHash,
            ILogger logger)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();
            return await ReserveWithContextAsync(context, db, tenantId, key, requestHash, logger);
        }

        private static async Task<Reservation> ReserveWithContextAsync(
            HttpContext context,
            PosDbContext db,
            Guid tenantId,
            string key,
            string requestHash,
            ILogger logger)
        {
            var existing = await db.IdempotencyEntries
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Key == key, context.RequestAborted);

            if (existing != null)
            {
                if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
                    return Reservation.Mismatch();

                if (existing.State == IdempotencyEntryState.Completed)
                    return Reservation.Replay(existing);

                if (existing.ProcessingExpiresAt > DateTime.UtcNow)
                    return Reservation.InFlight();

                await RemoveWithContextAsync(db, existing.Id);
                logger.LogWarning("Expired idempotency reservation was reclaimed for key {Key}", key);
            }

            var entry = new IdempotencyEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = key,
                Method = context.Request.Method,
                Path = GetRequestPath(context),
                RequestHash = requestHash,
                State = IdempotencyEntryState.Processing,
                CreatedAt = DateTime.UtcNow,
                ProcessingExpiresAt = DateTime.UtcNow.Add(ProcessingTtl)
            };

            db.IdempotencyEntries.Add(entry);
            try
            {
                await db.SaveChangesAsync(context.RequestAborted);
                return Reservation.Reserved(entry);
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                var raced = await db.IdempotencyEntries
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Key == key, context.RequestAborted);

                if (raced == null)
                    throw;

                if (!string.Equals(raced.RequestHash, requestHash, StringComparison.Ordinal))
                    return Reservation.Mismatch();

                return raced.State == IdempotencyEntryState.Completed
                    ? Reservation.Replay(raced)
                    : Reservation.InFlight();
            }
        }

        private async Task CompleteAsync(
            Guid id,
            int statusCode,
            string? contentType,
            string responseBody)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();
            await CompleteWithContextAsync(db, id, statusCode, contentType, responseBody);
        }

        private static Task CompleteWithContextAsync(
            PosDbContext db,
            Guid id,
            int statusCode,
            string? contentType,
            string responseBody)
        {
            return db.IdempotencyEntries
                .Where(e => e.Id == id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(e => e.State, IdempotencyEntryState.Completed)
                    .SetProperty(e => e.StatusCode, statusCode)
                    .SetProperty(e => e.ContentType, contentType)
                    .SetProperty(e => e.ResponseBody, responseBody)
                    .SetProperty(e => e.CompletedAt, DateTime.UtcNow));
        }

        private async Task RemoveAsync(Guid id)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();
            await RemoveWithContextAsync(db, id);
        }

        private static Task RemoveWithContextAsync(PosDbContext db, Guid id)
        {
            return db.IdempotencyEntries
                .Where(e => e.Id == id)
                .ExecuteDeleteAsync();
        }

        private static async Task ReplayAsync(HttpContext context, IdempotencyEntry entry)
        {
            context.Response.StatusCode = entry.StatusCode ?? StatusCodes.Status200OK;
            context.Response.ContentType = entry.ContentType ?? "application/json";
            if (!string.IsNullOrEmpty(entry.ResponseBody))
                await context.Response.WriteAsync(entry.ResponseBody, context.RequestAborted);
        }

        private static Task WriteConflictAsync(HttpContext context, string message)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            context.Response.ContentType = "application/json";
            return context.Response.WriteAsync($"{{\"message\":\"{message}\"}}", context.RequestAborted);
        }

        private static async Task<string> ComputeRequestHashAsync(HttpContext context)
        {
            var body = string.Empty;
            if (context.Request.ContentLength.GetValueOrDefault() > 0)
            {
                context.Request.EnableBuffering();
                using var reader = new StreamReader(
                    context.Request.Body,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: false,
                    leaveOpen: true);
                body = await reader.ReadToEndAsync(context.RequestAborted);
                context.Request.Body.Position = 0;
            }

            var raw = $"{context.Request.Method}|{GetRequestPath(context)}|{body}";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash);
        }

        private static string GetRequestPath(HttpContext context)
            => $"{context.Request.Path.Value}{context.Request.QueryString.Value}";

        private static bool TryGetKey(HttpContext context, out string key)
        {
            key = string.Empty;
            if (!context.Request.Headers.TryGetValue(HeaderName, out var values))
                return false;

            key = values.ToString().Trim();
            return key.Length is > 0 and <= 120;
        }

        private static bool ShouldHandle(HttpContext context)
        {
            if (!ScopedMethods.Contains(context.Request.Method))
                return false;

            var path = context.Request.Path.Value;
            return !string.IsNullOrEmpty(path) &&
                ScopedPathPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        private enum ReservationKind
        {
            Reserved,
            Replay,
            InFlight,
            Mismatch
        }

        private sealed record Reservation(ReservationKind Kind, IdempotencyEntry? Entry)
        {
            public static Reservation Reserved(IdempotencyEntry entry) => new(ReservationKind.Reserved, entry);
            public static Reservation Replay(IdempotencyEntry entry) => new(ReservationKind.Replay, entry);
            public static Reservation InFlight() => new(ReservationKind.InFlight, null);
            public static Reservation Mismatch() => new(ReservationKind.Mismatch, null);
        }
    }
}
