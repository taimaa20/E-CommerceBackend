namespace RestaurantPos.Api.Exceptions
{
    /// <summary>
    /// Thrown by services when an entity lookup returns no result.
    /// Mapped to HTTP 404 by <see cref="Middleware.ExceptionMiddleware"/>.
    /// Never return null from a service to signal "not found" — throw this instead.
    /// </summary>
    public class NotFoundException : Exception
    {
        public NotFoundException()
            : base("The requested resource was not found.") { }

        public NotFoundException(string message)
            : base(message) { }

        public NotFoundException(string entityName, object key)
            : base($"{entityName} with id '{key}' was not found.") { }
    }
}
