namespace RestaurantPos.Api.Exceptions
{
    /// <summary>
    /// Thrown when the caller IS authenticated but is not allowed to perform the action
    /// against this specific resource (e.g. a cashier trying to close another cashier's
    /// shift). Distinct from <see cref="UnauthorizedException"/> (401, missing/invalid
    /// credentials). Mapped to HTTP 403 by <see cref="Middleware.ExceptionMiddleware"/>.
    /// </summary>
    public class ForbiddenException : Exception
    {
        public ForbiddenException()
            : base("Forbidden.") { }

        public ForbiddenException(string message)
            : base(message) { }
    }
}
