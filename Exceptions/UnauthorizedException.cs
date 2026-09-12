namespace RestaurantPos.Api.Exceptions
{
    /// <summary>
    /// Thrown when a caller is authenticated but not allowed to perform the action,
    /// or when credentials are missing/invalid for a flow that requires them
    /// (e.g. login with bad password, expired refresh token).
    /// Mapped to HTTP 401 by <see cref="Middleware.ExceptionMiddleware"/>.
    /// For role-based denial after auth, prefer the [Authorize(Policy=...)] attribute (returns 403).
    /// </summary>
    public class UnauthorizedException : Exception
    {
        public UnauthorizedException()
            : base("Unauthorized.") { }

        public UnauthorizedException(string message)
            : base(message) { }
    }
}
