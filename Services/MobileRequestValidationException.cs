namespace RestaurantPos.Api.Services
{
    public class MobileRequestValidationException : Exception
    {
        public MobileRequestValidationException(string message) : base(message)
        {
        }
    }
}
