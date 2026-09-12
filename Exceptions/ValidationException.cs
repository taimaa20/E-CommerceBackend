namespace RestaurantPos.Api.Exceptions
{
    /// <summary>
    /// Thrown by services when input fails business-rule validation
    /// (uniqueness, cross-field constraints, illegal state transitions, etc.).
    /// Mapped to HTTP 400 by <see cref="Middleware.ExceptionMiddleware"/>.
    /// For simple boundary checks, prefer DataAnnotations on DTOs (handled by ASP.NET model binding).
    /// </summary>
    public class ValidationException : Exception
    {
        /// <summary>
        /// Optional per-field error map: { "fieldName": ["error1", "error2"] }.
        /// Surfaced in the 400 response body for client-side highlighting.
        /// </summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; }

        public string? Status { get; }

        public ValidationException(string message)
            : base(message)
        {
            Errors = null;
            Status = null;
        }

        public ValidationException(string message, string status)
            : base(message)
        {
            Errors = null;
            Status = status;
        }

        public ValidationException(string message, IReadOnlyDictionary<string, string[]> errors)
            : base(message)
        {
            Errors = errors;
            Status = null;
        }

        public ValidationException(IReadOnlyDictionary<string, string[]> errors)
            : base("One or more validation errors occurred.")
        {
            Errors = errors;
            Status = null;
        }
    }
}
