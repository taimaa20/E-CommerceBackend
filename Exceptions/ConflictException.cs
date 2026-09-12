namespace RestaurantPos.Api.Exceptions
{
    /// <summary>
    /// Thrown when an operation cannot proceed because the current state of a resource
    /// conflicts with the requested change (e.g. cancelling an already-paid order,
    /// double-opening a cashier shift, optimistic-concurrency violation).
    /// Mapped to HTTP 409 by <see cref="Middleware.ExceptionMiddleware"/>.
    /// </summary>
    public class ConflictException : Exception
    {
        public ConflictException()
            : base("The operation conflicts with the current state of the resource.") { }

        public ConflictException(string message)
            : base(message) { }
    }
}
