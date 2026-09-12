using System.Text.Json;
using RestaurantPos.Api.Exceptions;

namespace RestaurantPos.Api.Middleware
{
    /// <summary>
    /// Translates service-thrown typed exceptions into precise HTTP responses.
    ///
    /// Mapping:
    ///   <see cref="NotFoundException"/>     → 404
    ///   <see cref="ValidationException"/>   → 400 (with optional per-field errors map)
    ///   <see cref="UnauthorizedException"/> → 401
    ///   <see cref="ConflictException"/>     → 409
    ///   anything else                       → 500 (logged with stack trace; no internal details leaked)
    ///
    /// Why a middleware and not try/catch in every controller:
    ///  - Single place to change the error contract.
    ///  - Controllers stay thin (CLAUDE.md rule 5).
    ///  - Services throw "what happened" (NotFound), middleware decides "how the wire reports it" (404).
    /// </summary>
    public class ExceptionMiddleware
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (NotFoundException ex)
            {
                await WriteAsync(context, StatusCodes.Status404NotFound, new { error = ex.Message });
            }
            catch (ValidationException ex)
            {
                var body = new Dictionary<string, object?>
                {
                    ["error"] = ex.Message,
                    ["errors"] = ex.Errors
                };

                if (!string.IsNullOrWhiteSpace(ex.Status))
                {
                    body["status"] = ex.Status;
                }

                await WriteAsync(context, StatusCodes.Status400BadRequest, body);
            }
            catch (UnauthorizedException ex)
            {
                await WriteAsync(context, StatusCodes.Status401Unauthorized, new { error = ex.Message });
            }
            catch (ForbiddenException ex)
            {
                await WriteAsync(context, StatusCodes.Status403Forbidden, new { error = ex.Message });
            }
            catch (DuplicateExpenseInvoiceException ex)
            {
                await WriteAsync(context, StatusCodes.Status409Conflict, new
                {
                    error = ex.Message,
                    duplicateInvoice = ex.Warning
                });
            }
            catch (ShiftCloseBlockedException ex)
            {
                // 409 carrying the structured validation payload so the close-shift
                // popup can render per-status counts and the force-close affordance.
                await WriteAsync(context, StatusCodes.Status409Conflict, new
                {
                    error = ex.Message,
                    validation = ex.Validation,
                });
            }
            catch (ConflictException ex)
            {
                await WriteAsync(context, StatusCodes.Status409Conflict, new { error = ex.Message });
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                // Client disconnected mid-request — not a server error, no body to write.
                _logger.LogDebug("Request {Method} {Path} cancelled by client",
                    context.Request.Method, context.Request.Path);
            }
            catch (Exception ex)
            {
                // Unexpected: log full detail server-side, return generic message client-side.
                _logger.LogError(ex, "Unhandled exception on {Method} {Path}",
                    context.Request.Method, context.Request.Path);

                await WriteAsync(context, StatusCodes.Status500InternalServerError, new
                {
                    error = "An unexpected error occurred while processing your request."
                });
            }
        }

        private static async Task WriteAsync(HttpContext context, int statusCode, object body)
        {
            if (context.Response.HasStarted)
            {
                // Response already begun streaming — too late to rewrite headers/body.
                return;
            }

            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
        }
    }
}
